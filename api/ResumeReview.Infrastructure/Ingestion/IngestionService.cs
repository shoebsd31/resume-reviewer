using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ResumeReview.Domain;
using ResumeReview.Infrastructure.AiEnrichment;
using ResumeReview.Infrastructure.Parsing;
using ResumeReview.Infrastructure.Persistence;

namespace ResumeReview.Infrastructure.Ingestion;

public interface IIngestionService
{
    Task<IngestionResult> IngestAsync(Stream docxStream, string sourceFileName, CancellationToken ct = default);
}

public class IngestionService : IIngestionService
{
    private readonly ResumeReviewDbContext _db;
    private readonly IResumeDocumentParser _parser;
    private readonly IEnrichmentQueue _queue;
    private readonly ILogger<IngestionService> _logger;

    public IngestionService(
        ResumeReviewDbContext db,
        IResumeDocumentParser parser,
        IEnrichmentQueue queue,
        ILogger<IngestionService> logger)
    {
        _db = db;
        _parser = parser;
        _queue = queue;
        _logger = logger;
    }

    public async Task<IngestionResult> IngestAsync(Stream docxStream, string sourceFileName, CancellationToken ct = default)
    {
        ParsedResume parsed;
        try
        {
            parsed = _parser.Parse(docxStream, sourceFileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse {File}", sourceFileName);
            return new IngestionResult(Guid.NewGuid(), Guid.Empty, false, $"parse_failed: {ex.Message}");
        }

        if (string.IsNullOrWhiteSpace(parsed.FullName))
            return new IngestionResult(Guid.NewGuid(), Guid.Empty, false, "missing_required_field: FullName");
        if (string.IsNullOrWhiteSpace(parsed.Email))
            return new IngestionResult(Guid.NewGuid(), Guid.Empty, false, "missing_required_field: Email");

        var existingId = await _db.Candidates
            .AsNoTracking()
            .Where(c => c.FullName == parsed.FullName && c.Email == parsed.Email)
            .Select(c => (Guid?)c.Id)
            .FirstOrDefaultAsync(ct);

        var isNew = existingId is null;
        Candidate candidate;
        if (isNew)
        {
            candidate = new Candidate();
        }
        else
        {
            await _db.Skills.Where(x => x.CandidateId == existingId).ExecuteDeleteAsync(ct);
            await _db.WorkExperiences.Where(x => x.CandidateId == existingId).ExecuteDeleteAsync(ct);
            await _db.EducationEntries.Where(x => x.CandidateId == existingId).ExecuteDeleteAsync(ct);
            await _db.Certifications.Where(x => x.CandidateId == existingId).ExecuteDeleteAsync(ct);
            await _db.Projects.Where(x => x.CandidateId == existingId).ExecuteDeleteAsync(ct);
            // ExecuteDelete bypasses the change tracker. Reset so reloaded navigation collections start empty.
            _db.ChangeTracker.Clear();
            candidate = await _db.Candidates.FirstAsync(c => c.Id == existingId, ct);
        }

        candidate.FullName = parsed.FullName;
        candidate.Email = parsed.Email;
        candidate.Phone = parsed.Phone;
        candidate.Location = parsed.Location;
        candidate.LinkedInUrl = parsed.LinkedInUrl;
        candidate.GitHubUrl = parsed.GitHubUrl;
        candidate.Summary = parsed.Summary;
        candidate.SourceFileName = sourceFileName;

        foreach (var s in parsed.Skills) { s.CandidateId = candidate.Id; _db.Skills.Add(s); }
        foreach (var e in parsed.Experiences) { e.CandidateId = candidate.Id; _db.WorkExperiences.Add(e); }
        foreach (var e in parsed.Education) { e.CandidateId = candidate.Id; _db.EducationEntries.Add(e); }
        foreach (var c in parsed.Certifications) { c.CandidateId = candidate.Id; _db.Certifications.Add(c); }
        foreach (var p in parsed.Projects) { p.CandidateId = candidate.Id; _db.Projects.Add(p); }

        if (isNew) _db.Candidates.Add(candidate);

        await _db.SaveChangesAsync(ct);

        var ingestionId = Guid.NewGuid();
        await _queue.EnqueueAsync(new EnrichmentJob(candidate.Id, RequestedBy: "ingestion"), ct);
        _logger.LogInformation("Ingested candidate {CandidateId} (new={IsNew}) from {File}",
            candidate.Id, isNew, sourceFileName);
        return new IngestionResult(ingestionId, candidate.Id, isNew);
    }

}
