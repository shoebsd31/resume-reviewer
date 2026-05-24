using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using ResumeReview.Domain;

namespace ResumeReview.Infrastructure.Parsing;

public record ParsedResume(
    string FullName,
    string Email,
    string? Phone,
    string? Location,
    string? LinkedInUrl,
    string? GitHubUrl,
    string Summary,
    List<Skill> Skills,
    List<WorkExperience> Experiences,
    List<Education> Education,
    List<Certification> Certifications,
    List<Project> Projects,
    IDictionary<string, string> RawControls);

public interface IResumeDocumentParser
{
    ParsedResume Parse(Stream docxStream, string sourceFileName);
}

public class ResumeDocumentParser : IResumeDocumentParser
{
    public ParsedResume Parse(Stream docxStream, string sourceFileName)
    {
        using var doc = WordprocessingDocument.Open(docxStream, false);
        var body = doc.MainDocumentPart?.Document?.Body
                   ?? throw new InvalidOperationException("Document body missing");

        var byTag = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var sdt in body.Descendants<SdtElement>())
        {
            var tag = sdt.SdtProperties?.GetFirstChild<Tag>()?.Val?.Value;
            if (string.IsNullOrWhiteSpace(tag)) continue;
            var text = ExtractText(sdt);
            if (!byTag.ContainsKey(tag))
            {
                byTag[tag] = text;
            }
        }

        // Fallback: many simple .docx files don't contain content controls.
        // Parse the document as a flat resume in that case.
        if (byTag.Count == 0)
        {
            return ParseFromPlainText(body, sourceFileName);
        }

        var name = Get(byTag, "name", "FullName") ?? Path.GetFileNameWithoutExtension(sourceFileName);
        var contact = Get(byTag, "contact") ?? string.Empty;
        var email = ExtractEmail(contact) ?? $"{Slug(name)}@example.com";
        var phone = ExtractPhone(contact);
        var location = ExtractLocation(contact);
        var linkedIn = ExtractUrl(contact, "linkedin");
        var github = ExtractUrl(contact, "github");
        var title = Get(byTag, "title") ?? string.Empty;
        var summary = string.IsNullOrWhiteSpace(title)
            ? Get(byTag, "summary", "Summary") ?? string.Empty
            : title;

        var skills = SplitList(Get(byTag, "skills"))
            .Select((s, i) => new Skill { Name = s, OrderIndex = i })
            .ToList();

        var experiences = ParseExperiences(Get(byTag, "experience"));
        var education = ParseEducation(Get(byTag, "Education", "education"));
        var awards = SplitList(Get(byTag, "awards"))
            .Select((s, i) => new Certification { Name = s, Issuer = "Self-reported", OrderIndex = i })
            .ToList();

        return new ParsedResume(
            name.Trim(), email, phone, location, linkedIn, github, summary,
            skills, experiences, education, awards, new List<Project>(), byTag);
    }

    private static ParsedResume ParseFromPlainText(Body body, string sourceFileName)
    {
        var sb = new StringBuilder();
        foreach (var p in body.Descendants<Paragraph>())
        {
            sb.AppendLine(p.InnerText);
        }
        var text = sb.ToString();
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                        .Select(l => l.Trim())
                        .Where(l => l.Length > 0)
                        .ToList();
        var name = lines.FirstOrDefault() ?? Path.GetFileNameWithoutExtension(sourceFileName);
        var email = ExtractEmail(text) ?? $"{Slug(name)}@example.com";
        return new ParsedResume(name, email, null, null, null, null, text,
            new List<Skill>(), new List<WorkExperience>(), new List<Education>(),
            new List<Certification>(), new List<Project>(), new Dictionary<string, string>());
    }

    private static string ExtractText(SdtElement sdt)
    {
        var sb = new StringBuilder();
        foreach (var t in sdt.Descendants<Text>()) sb.Append(t.Text);
        // Preserve paragraph breaks within block-level controls.
        if (sdt is SdtBlock)
        {
            sb.Clear();
            foreach (var p in sdt.Descendants<Paragraph>())
            {
                var line = string.Concat(p.Descendants<Text>().Select(t => t.Text));
                if (!string.IsNullOrWhiteSpace(line)) sb.AppendLine(line);
            }
        }
        return sb.ToString().Trim();
    }

    private static string? Get(IDictionary<string, string> d, params string[] keys)
        => keys.Select(k => d.TryGetValue(k, out var v) ? v : null)
               .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

    private static IEnumerable<string> SplitList(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) yield break;
        foreach (var token in raw.Split(new[] { '\n', ',', ';', '|', '•' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var t = token.Trim().Trim('-', '*').Trim();
            if (!string.IsNullOrWhiteSpace(t)) yield return t;
        }
    }

    private static List<WorkExperience> ParseExperiences(string? raw)
    {
        var result = new List<WorkExperience>();
        if (string.IsNullOrWhiteSpace(raw)) return result;
        var idx = 0;
        foreach (var block in raw.Split(new[] { "\n\n", "\r\n\r\n" }, StringSplitOptions.RemoveEmptyEntries))
        {
            var lines = block.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                             .Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
            if (lines.Count == 0) continue;
            var header = lines[0];
            var parts = header.Split(new[] { " @ ", " at ", " — ", " - ", "|" }, 2, StringSplitOptions.None);
            var title = parts.Length > 0 ? parts[0].Trim() : header;
            var rest = parts.Length > 1 ? parts[1].Trim() : "";
            var company = rest;
            DateOnly start = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-1));
            DateOnly? end = null;
            if (lines.Count > 1)
            {
                var (s, e) = ParseDateRange(lines[1]);
                start = s ?? start;
                end = e;
            }
            var desc = lines.Count > 2 ? string.Join("\n", lines.Skip(2)) : "";
            result.Add(new WorkExperience
            {
                Title = title, Company = company, StartDate = start, EndDate = end,
                Description = desc, OrderIndex = idx++
            });
        }
        return result;
    }

    private static List<Education> ParseEducation(string? raw)
    {
        var result = new List<Education>();
        if (string.IsNullOrWhiteSpace(raw)) return result;
        var idx = 0;
        foreach (var line in raw.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed)) continue;
            var parts = trimmed.Split(new[] { ", ", " — ", " - ", "|" }, StringSplitOptions.None)
                               .Select(p => p.Trim()).ToList();
            var inst = parts.ElementAtOrDefault(0) ?? trimmed;
            var deg = parts.ElementAtOrDefault(1) ?? "";
            var field = parts.ElementAtOrDefault(2) ?? "";
            int? year = null;
            foreach (var p in parts)
            {
                if (int.TryParse(p, out var y) && y >= 1950 && y <= 2100) { year = y; break; }
            }
            result.Add(new Education
            {
                Institution = inst, Degree = deg, Field = field, GraduationYear = year, OrderIndex = idx++
            });
        }
        return result;
    }

    private static (DateOnly? start, DateOnly? end) ParseDateRange(string s)
    {
        var parts = s.Split(new[] { " - ", " – ", " to ", "-" }, 2, StringSplitOptions.None);
        DateOnly? start = TryParseDate(parts.ElementAtOrDefault(0));
        var endStr = parts.ElementAtOrDefault(1)?.Trim() ?? "";
        DateOnly? end = endStr.Equals("Present", StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(endStr)
            ? null : TryParseDate(endStr);
        return (start, end);
    }

    private static DateOnly? TryParseDate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var s = raw.Trim();
        var formats = new[] { "yyyy-MM", "yyyy-MM-dd", "MMM yyyy", "MMMM yyyy", "yyyy" };
        if (DateOnly.TryParseExact(s, formats, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var d)) return d;
        if (int.TryParse(s, out var year) && year is >= 1950 and <= 2100)
            return new DateOnly(year, 1, 1);
        if (DateTime.TryParse(s, out var dt)) return DateOnly.FromDateTime(dt);
        return null;
    }

    private static string? ExtractEmail(string text)
    {
        var m = System.Text.RegularExpressions.Regex.Match(text, @"[\w\.\-+]+@[\w\.\-]+\.[A-Za-z]{2,}");
        return m.Success ? m.Value : null;
    }

    private static string? ExtractPhone(string text)
    {
        var m = System.Text.RegularExpressions.Regex.Match(text, @"(\+?\d[\d\s\-\(\)]{6,}\d)");
        return m.Success ? m.Value.Trim() : null;
    }

    private static string? ExtractLocation(string text)
    {
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        return lines.FirstOrDefault(l =>
        {
            var s = l.Trim();
            return s.Length is > 2 and < 80
                && !s.Contains('@') && !System.Text.RegularExpressions.Regex.IsMatch(s, @"\d{5,}")
                && s.Contains(',');
        })?.Trim();
    }

    private static string? ExtractUrl(string text, string keyword)
    {
        var pattern = $@"https?://[^\s]*{keyword}[^\s]*";
        var m = System.Text.RegularExpressions.Regex.Match(text, pattern,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return m.Success ? m.Value : null;
    }

    private static string Slug(string s) => System.Text.RegularExpressions.Regex
        .Replace(s.ToLowerInvariant(), @"[^a-z0-9]+", ".").Trim('.');
}
