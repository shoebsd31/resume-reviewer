using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ResumeReview.Infrastructure.AiEnrichment;
using ResumeReview.Infrastructure.Ingestion;
using ResumeReview.Infrastructure.Parsing;
using ResumeReview.Tests.TestHelpers;

namespace ResumeReview.Tests.Ingestion;

public class IngestionServiceTests
{
    [Fact]
    public async Task Ingest_creates_candidate_and_enqueues_enrichment()
    {
        using var test = new TestDbContext();
        var queue = new ChannelEnrichmentQueue();
        var svc = new IngestionService(test.Db, new ResumeDocumentParser(), queue, NullLogger<IngestionService>.Instance);

        using var docx = TestDocxBuilder.BuildDocx();
        var result = await svc.IngestAsync(docx, "ada.docx");

        result.Success.Should().BeTrue();
        result.IsNew.Should().BeTrue();
        var c = await test.Db.Candidates.Include(c => c.Skills).Include(c => c.WorkExperiences).FirstAsync();
        c.FullName.Should().Be("Ada Lovelace");
        c.Skills.Should().HaveCount(5);
        c.WorkExperiences.Should().HaveCountGreaterThan(0);

        // queue should have one item
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var enumerator = queue.ReadAllAsync(cts.Token).GetAsyncEnumerator();
        (await enumerator.MoveNextAsync()).Should().BeTrue();
        enumerator.Current.CandidateId.Should().Be(c.Id);
    }

    [Fact]
    public async Task Ingest_is_idempotent_by_name_and_email()
    {
        using var test = new TestDbContext();
        var svc = new IngestionService(test.Db, new ResumeDocumentParser(), new ChannelEnrichmentQueue(), NullLogger<IngestionService>.Instance);

        using var docx1 = TestDocxBuilder.BuildDocx(skills: "Python, AWS");
        var r1 = await svc.IngestAsync(docx1, "v1.docx");
        using var docx2 = TestDocxBuilder.BuildDocx(skills: "Python, AWS, GCP, Terraform");
        var r2 = await svc.IngestAsync(docx2, "v2.docx");

        r1.IsNew.Should().BeTrue();
        r2.IsNew.Should().BeFalse();
        r1.CandidateId.Should().Be(r2.CandidateId);

        var c = await test.Db.Candidates.Include(c => c.Skills).SingleAsync();
        c.Skills.Should().HaveCount(4);
        c.SourceFileName.Should().Be("v2.docx");
    }

    [Fact]
    public async Task Ingest_rejects_unparseable_input()
    {
        using var test = new TestDbContext();
        var svc = new IngestionService(test.Db, new ResumeDocumentParser(), new ChannelEnrichmentQueue(), NullLogger<IngestionService>.Instance);

        using var bogus = new MemoryStream(new byte[] { 0x01, 0x02, 0x03, 0x04 });
        var result = await svc.IngestAsync(bogus, "garbage.docx");
        result.Success.Should().BeFalse();
        result.Error.Should().StartWith("parse_failed");
    }
}
