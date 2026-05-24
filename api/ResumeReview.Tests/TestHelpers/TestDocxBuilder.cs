using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace ResumeReview.Tests.TestHelpers;

public static class TestDocxBuilder
{
    public static MemoryStream BuildDocx(
        string name = "Ada Lovelace",
        string email = "ada@example.com",
        string title = "Senior ML Engineer",
        string skills = "Python, PyTorch, Hugging Face, Kubernetes, AWS",
        string experience = "Senior ML Engineer @ Acme AI\n2022-01 - Present\nLed model serving.\n\nML Engineer @ Initech\n2019-06 - 2021-12\nBuilt training pipeline.",
        string education = "MIT, MSc, Computer Science, 2018",
        string awards = "Best paper, ML4H 2024",
        string contactExtras = "")
    {
        var contact = $"{email}\n+1 555-123-4567\nLondon, UK\nhttps://www.linkedin.com/in/ada\nhttps://github.com/ada\n{contactExtras}";
        var ms = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document, autoSave: false))
        {
            var main = doc.AddMainDocumentPart();
            var body = new Body(
                TaggedBlock("name", name),
                TaggedBlock("title", title),
                TaggedBlock("contact", contact),
                TaggedBlock("skills", skills),
                TaggedBlock("experience", experience),
                TaggedBlock("Education", education),
                TaggedBlock("awards", awards));
            main.Document = new Document(body);
            main.Document.Save();
        }
        ms.Position = 0;
        return ms;
    }

    private static SdtBlock TaggedBlock(string tag, string text)
    {
        var paragraphs = (text ?? string.Empty)
            .Split('\n')
            .Select(line => new Paragraph(new Run(new Text(line.TrimEnd('\r')) { Space = SpaceProcessingModeValues.Preserve })))
            .Cast<OpenXmlElement>().ToArray();
        return new SdtBlock(
            new SdtProperties(new Tag { Val = tag }, new SdtAlias { Val = tag }),
            new SdtContentBlock(paragraphs));
    }
}
