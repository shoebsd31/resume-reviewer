using FluentAssertions;
using ResumeReview.Infrastructure.Parsing;
using ResumeReview.Tests.TestHelpers;

namespace ResumeReview.Tests.Parsing;

public class ResumeDocumentParserTests
{
    [Fact]
    public void Parse_extracts_name_email_skills_and_experience()
    {
        using var ms = TestDocxBuilder.BuildDocx();
        var parser = new ResumeDocumentParser();
        var parsed = parser.Parse(ms, "ada.docx");

        parsed.FullName.Should().Be("Ada Lovelace");
        parsed.Email.Should().Be("ada@example.com");
        parsed.Skills.Should().HaveCount(5);
        parsed.Skills.Select(s => s.Name).Should().Contain("PyTorch");
        parsed.Experiences.Should().HaveCountGreaterThan(0);
        parsed.Experiences[0].Title.Should().Contain("ML Engineer");
        parsed.Education.Should().NotBeEmpty();
        parsed.Education[0].Institution.Should().Be("MIT");
        parsed.Certifications.Should().NotBeEmpty();
    }

    [Fact]
    public void Parse_assigns_order_indexes()
    {
        using var ms = TestDocxBuilder.BuildDocx(skills: "A, B, C, D");
        var parser = new ResumeDocumentParser();
        var parsed = parser.Parse(ms, "x.docx");
        parsed.Skills.Select(s => s.OrderIndex).Should().Equal(0, 1, 2, 3);
    }
}
