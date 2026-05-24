using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace ResumeReview.SampleGenerator;

public class ResumeGenerator
{
    private readonly string? _templatePath;
    private readonly int? _seed;

    public ResumeGenerator(string? templatePath, int? seed)
    {
        _templatePath = templatePath;
        _seed = seed;
    }

    public void GenerateTo(CandidateProfile profile, string outPath)
    {
        if (string.IsNullOrWhiteSpace(_templatePath) || !File.Exists(_templatePath))
        {
            throw new FileNotFoundException(
                $"Template '{_templatePath}' not found. Pass --template <path-to-resumetemplate.dotx>.",
                _templatePath);
        }

        File.Copy(_templatePath, outPath, overwrite: true);

        using var doc = WordprocessingDocument.Open(outPath, isEditable: true);
        doc.ChangeDocumentType(WordprocessingDocumentType.Document);

        var body = doc.MainDocumentPart?.Document?.Body
                   ?? throw new InvalidOperationException("Document body missing in template");

        var fields = BuildFieldValues(profile);

        foreach (var sdt in body.Descendants<SdtBlock>().ToList())
        {
            var tag = sdt.SdtProperties?.GetFirstChild<Tag>()?.Val?.Value;
            if (string.IsNullOrWhiteSpace(tag) || !fields.TryGetValue(tag, out var text)) continue;
            ReplaceBlockContent(sdt, text);
        }

        foreach (var sdt in body.Descendants<SdtRun>().ToList())
        {
            var tag = sdt.SdtProperties?.GetFirstChild<Tag>()?.Val?.Value;
            if (string.IsNullOrWhiteSpace(tag) || !fields.TryGetValue(tag, out var text)) continue;
            ReplaceRunContent(sdt, text);
        }

        doc.MainDocumentPart!.Document.Save();
    }

    private static Dictionary<string, string> BuildFieldValues(CandidateProfile p)
    {
        var experiences = new StringBuilder();
        foreach (var e in p.Experiences)
        {
            var end = e.End is null ? "Present" : e.End.Value.ToString("yyyy-MM");
            experiences.AppendLine($"{e.Title} @ {e.Company}");
            experiences.AppendLine($"{e.Start:yyyy-MM} - {end}");
            experiences.AppendLine(e.Description);
            experiences.AppendLine();
        }
        var education = new StringBuilder();
        foreach (var ed in p.Education)
            education.AppendLine($"{ed.Institution}, {ed.Degree}, {ed.Field}, {ed.Year}");

        var contact = string.Join("\n",
            new[] { p.Email, p.Phone, p.Location, p.LinkedIn, p.GitHub }
                .Where(s => !string.IsNullOrWhiteSpace(s)));

        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["name"] = p.FullName,
            ["title"] = p.Title,
            ["contact"] = contact,
            ["skills"] = string.Join("\n", p.Skills),
            ["experience"] = experiences.ToString().TrimEnd(),
            ["Education"] = education.ToString().TrimEnd(),
            ["awards"] = string.Join("\n", p.Awards),
        };
    }

    private static void ReplaceBlockContent(SdtBlock sdt, string newText)
    {
        var content = sdt.GetFirstChild<SdtContentBlock>();
        if (content is null) return;

        var (firstPpr, firstRpr, bodyPpr, bodyRpr) = CaptureAnchors(content);

        var lines = (newText ?? string.Empty).Replace("\r", string.Empty).Split('\n');
        if (lines.Length == 0) lines = new[] { string.Empty };

        content.RemoveAllChildren();
        for (var i = 0; i < lines.Length; i++)
        {
            var ppr = i == 0 ? firstPpr : bodyPpr;
            var rpr = i == 0 ? firstRpr : bodyRpr;
            content.AppendChild(BuildParagraph(lines[i], ppr, rpr));
        }
    }

    private static void ReplaceRunContent(SdtRun sdt, string newText)
    {
        var content = sdt.GetFirstChild<SdtContentRun>();
        if (content is null) return;
        var rpr = content.Descendants<Run>().FirstOrDefault()
                         ?.GetFirstChild<RunProperties>()?.CloneNode(true) as RunProperties;
        content.RemoveAllChildren();
        var run = new Run();
        if (rpr is not null) run.AppendChild(rpr);
        run.AppendChild(new Text(newText ?? string.Empty) { Space = SpaceProcessingModeValues.Preserve });
        content.AppendChild(run);
    }

    private static (ParagraphProperties? firstPpr, RunProperties? firstRpr,
                    ParagraphProperties? bodyPpr, RunProperties? bodyRpr) CaptureAnchors(OpenXmlElement content)
    {
        var paras = content.Descendants<Paragraph>().ToList();
        var firstPpr = paras.ElementAtOrDefault(0)?.GetFirstChild<ParagraphProperties>()?.CloneNode(true) as ParagraphProperties;
        var firstRpr = paras.ElementAtOrDefault(0)?.Descendants<Run>().FirstOrDefault()
                            ?.GetFirstChild<RunProperties>()?.CloneNode(true) as RunProperties;
        var bodyPpr = paras.ElementAtOrDefault(1)?.GetFirstChild<ParagraphProperties>()?.CloneNode(true) as ParagraphProperties
                      ?? StripHeading(firstPpr);
        var bodyRpr = paras.ElementAtOrDefault(1)?.Descendants<Run>().FirstOrDefault()
                            ?.GetFirstChild<RunProperties>()?.CloneNode(true) as RunProperties
                      ?? firstRpr;
        return (firstPpr, firstRpr, bodyPpr, bodyRpr);
    }

    private static ParagraphProperties? StripHeading(ParagraphProperties? src)
    {
        if (src is null) return null;
        var clone = (ParagraphProperties)src.CloneNode(true);
        clone.GetFirstChild<ParagraphStyleId>()?.Remove();
        return clone;
    }

    private static Paragraph BuildParagraph(string text, ParagraphProperties? ppr, RunProperties? rpr)
    {
        var p = new Paragraph();
        if (ppr is not null) p.AppendChild(ppr.CloneNode(true));
        var run = new Run();
        if (rpr is not null) run.AppendChild(rpr.CloneNode(true));
        run.AppendChild(new Text(text ?? string.Empty) { Space = SpaceProcessingModeValues.Preserve });
        p.AppendChild(run);
        return p;
    }
}
