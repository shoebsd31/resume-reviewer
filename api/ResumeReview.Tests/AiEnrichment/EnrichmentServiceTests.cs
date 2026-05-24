using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ResumeReview.Domain;
using ResumeReview.Infrastructure.AiEnrichment;
using ResumeReview.Tests.TestHelpers;

namespace ResumeReview.Tests.AiEnrichment;

public class EnrichmentServiceTests
{
    private static Candidate SeedCandidate(TestDbContext test)
    {
        var c = new Candidate
        {
            FullName = "Grace Hopper",
            Email = "grace@example.com",
            SourceFileName = "g.docx",
            Skills =
            {
                new Skill { Name = "Python", OrderIndex = 0 },
                new Skill { Name = "AWS", OrderIndex = 1 },
                new Skill { Name = "PyTorch", OrderIndex = 2 }
            },
            WorkExperiences =
            {
                new WorkExperience { Title = "Senior ML Engineer", Company = "Acme",
                    StartDate = new DateOnly(2019, 1, 1), EndDate = new DateOnly(2024, 1, 1),
                    Description = "Shipped LLMs.", OrderIndex = 0 }
            }
        };
        test.Db.Candidates.Add(c);
        test.Db.SaveChanges();
        return c;
    }

    private static EnrichmentService MakeService(TestDbContext test) =>
        new(test.Db, new StubAiProvider(),
            Options.Create(new WorkerOptions { RetryMaxAttempts = 1 }),
            NullLogger<EnrichmentService>.Instance);

    [Fact]
    public async Task EnrichCandidate_populates_all_ai_fields_and_history()
    {
        using var test = new TestDbContext();
        var c = SeedCandidate(test);
        var svc = MakeService(test);

        await svc.EnrichCandidateAsync(c.Id);

        var ai = await test.Db.CandidateAiFields.FirstAsync();
        ai.AiSummary.Should().NotBeNullOrWhiteSpace();
        ai.AiSeniorityLevel.Should().NotBeNullOrEmpty();
        ai.AiTopStrengths.Should().Contain("Python");
        ai.EnrichmentStatus.Should().Be(EnrichmentStatus.Completed);

        var history = await test.Db.AiGenerationHistory.CountAsync();
        history.Should().Be(AiFieldNames.All.Length);

        var overrides = await test.Db.CandidateAiFieldOverrides.CountAsync();
        overrides.Should().Be(AiFieldNames.All.Length);
    }

    [Fact]
    public async Task RegenerateField_updates_value_and_appends_history()
    {
        using var test = new TestDbContext();
        var c = SeedCandidate(test);
        var svc = MakeService(test);

        await svc.EnrichCandidateAsync(c.Id);
        var before = await test.Db.AiGenerationHistory.CountAsync(h => h.FieldName == AiFieldNames.AiSummary);

        var result = await svc.RegenerateFieldAsync(c.Id, AiFieldNames.AiSummary, "make it more concise", "tester");

        result.NewValue.Should().NotBeNullOrEmpty();
        (await test.Db.AiGenerationHistory.CountAsync(h => h.FieldName == AiFieldNames.AiSummary))
            .Should().Be(before + 1);

        var overrideRow = await test.Db.CandidateAiFieldOverrides.FirstAsync(o => o.FieldName == AiFieldNames.AiSummary);
        overrideRow.CurrentValue.Should().Be(result.NewValue);
        overrideRow.OriginalAiValue.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task UserEdit_then_Revert_round_trip()
    {
        using var test = new TestDbContext();
        var c = SeedCandidate(test);
        var svc = MakeService(test);
        await svc.EnrichCandidateAsync(c.Id);

        await svc.ApplyUserEditAsync(c.Id, AiFieldNames.AiSummary, "Custom user text", "user");
        var ai = await test.Db.CandidateAiFields.FirstAsync();
        ai.AiSummary.Should().Be("Custom user text");

        var overrideRow = await test.Db.CandidateAiFieldOverrides.FirstAsync(o => o.FieldName == AiFieldNames.AiSummary);
        overrideRow.IsUserEdited.Should().BeTrue();

        await svc.RevertFieldAsync(c.Id, AiFieldNames.AiSummary, "user");
        var ai2 = await test.Db.CandidateAiFields.AsNoTracking().FirstAsync();
        ai2.AiSummary.Should().Be(overrideRow.OriginalAiValue);
    }
}
