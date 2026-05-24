using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ResumeReview.Domain;
using ResumeReview.Infrastructure.Persistence;

namespace ResumeReview.Infrastructure.AiEnrichment;

public class WorkerOptions
{
    public int MaxConcurrentEnrichments { get; set; } = 4;
    public int RetryMaxAttempts { get; set; } = 3;
    public int RetryBaseDelaySeconds { get; set; } = 2;
}

public interface IEnrichmentService
{
    Task EnrichCandidateAsync(Guid candidateId, string? requestedBy = null, CancellationToken ct = default);
    Task<RegenerationResult> RegenerateFieldAsync(Guid candidateId, string fieldName, string? extraInstructions, string requestedBy, CancellationToken ct = default);
}

public record RegenerationResult(string NewValue, Guid HistoryId);

public class EnrichmentService : IEnrichmentService
{
    private readonly ResumeReviewDbContext _db;
    private readonly IAiProvider _provider;
    private readonly ILogger<EnrichmentService> _logger;
    private readonly WorkerOptions _opts;

    public EnrichmentService(
        ResumeReviewDbContext db,
        IAiProvider provider,
        IOptions<WorkerOptions> opts,
        ILogger<EnrichmentService> logger)
    {
        _db = db;
        _provider = provider;
        _opts = opts.Value;
        _logger = logger;
    }

    public async Task EnrichCandidateAsync(Guid candidateId, string? requestedBy = null, CancellationToken ct = default)
    {
        var candidate = await LoadCandidateAsync(candidateId, ct);
        if (candidate is null)
        {
            _logger.LogWarning("Candidate {Id} not found for enrichment", candidateId);
            return;
        }

        var aiFields = await EnsureAiFieldsAsync(candidateId, ct);
        aiFields.EnrichmentStatus = EnrichmentStatus.InProgress;
        await _db.SaveChangesAsync(ct);

        var allOk = true;
        foreach (var field in AiFieldNames.All)
        {
            var ok = await GenerateAndStoreAsync(candidate, aiFields, field, null, requestedBy ?? "worker", ct);
            if (!ok) allOk = false;
        }

        aiFields.EnrichmentStatus = allOk ? EnrichmentStatus.Completed : EnrichmentStatus.Failed;
        aiFields.LastEnrichedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<RegenerationResult> RegenerateFieldAsync(
        Guid candidateId, string fieldName, string? extraInstructions, string requestedBy, CancellationToken ct = default)
    {
        if (!AiFieldNames.All.Contains(fieldName))
            throw new ArgumentException($"Unknown AI field: {fieldName}");

        var candidate = await LoadCandidateAsync(candidateId, ct)
            ?? throw new InvalidOperationException("Candidate not found");

        var aiFields = await EnsureAiFieldsAsync(candidateId, ct);
        var ok = await GenerateAndStoreAsync(candidate, aiFields, fieldName, extraInstructions, requestedBy, ct);
        if (!ok) throw new InvalidOperationException($"Regeneration failed for {fieldName}");
        await _db.SaveChangesAsync(ct);

        var historyRow = await _db.AiGenerationHistory
            .Where(h => h.CandidateId == candidateId && h.FieldName == fieldName)
            .OrderByDescending(h => h.RequestedAt)
            .FirstAsync(ct);

        return new RegenerationResult(historyRow.ResponseText, historyRow.Id);
    }

    private async Task<bool> GenerateAndStoreAsync(
        Candidate candidate,
        CandidateAiFields aiFields,
        string fieldName,
        string? extraInstructions,
        string requestedBy,
        CancellationToken ct)
    {
        var attempts = 0;
        Exception? lastEx = null;
        while (attempts < _opts.RetryMaxAttempts)
        {
            attempts++;
            try
            {
                var result = await _provider.GenerateAsync(fieldName, candidate, extraInstructions, ct);
                ApplyToAiFields(aiFields, fieldName, result.Value);
                _db.AiGenerationHistory.Add(new AiGenerationHistory
                {
                    CandidateId = candidate.Id,
                    FieldName = fieldName,
                    ModelName = result.ModelName,
                    PromptText = result.Prompt,
                    ExtraInstructions = extraInstructions,
                    ResponseText = result.Value,
                    LatencyMs = result.LatencyMs,
                    TokenUsage = result.TokenUsage,
                    RequestedBy = requestedBy,
                    Status = GenerationStatus.Success
                });
                await UpsertOverrideAsync(candidate.Id, fieldName, result.Value, requestedBy, isUserEdit: false, ct);
                return true;
            }
            catch (Exception ex)
            {
                lastEx = ex;
                _logger.LogWarning(ex, "Enrichment attempt {Attempt} failed for {Candidate}/{Field}",
                    attempts, candidate.Id, fieldName);
                var delay = TimeSpan.FromSeconds(Math.Min(30, _opts.RetryBaseDelaySeconds * Math.Pow(2, attempts - 1)));
                if (attempts < _opts.RetryMaxAttempts) await Task.Delay(delay, ct);
            }
        }

        _db.AiGenerationHistory.Add(new AiGenerationHistory
        {
            CandidateId = candidate.Id,
            FieldName = fieldName,
            ModelName = _provider.ModelName,
            PromptText = "(failed)",
            ExtraInstructions = extraInstructions,
            ResponseText = string.Empty,
            LatencyMs = 0,
            RequestedBy = requestedBy,
            Status = GenerationStatus.Failure,
            ErrorMessage = lastEx?.Message
        });
        aiFields.LastError = lastEx?.Message;
        return false;
    }

    private async Task UpsertOverrideAsync(Guid candidateId, string fieldName, string value, string updatedBy, bool isUserEdit, CancellationToken ct)
    {
        var existing = await _db.CandidateAiFieldOverrides
            .FirstOrDefaultAsync(o => o.CandidateId == candidateId && o.FieldName == fieldName, ct);
        if (existing is null)
        {
            _db.CandidateAiFieldOverrides.Add(new CandidateAiFieldOverride
            {
                CandidateId = candidateId,
                FieldName = fieldName,
                OriginalAiValue = value,
                CurrentValue = value,
                IsUserEdited = isUserEdit,
                LastEditedBy = updatedBy
            });
        }
        else
        {
            existing.CurrentValue = value;
            existing.IsUserEdited = isUserEdit;
            existing.LastEditedBy = updatedBy;
        }
    }

    public async Task ApplyUserEditAsync(Guid candidateId, string fieldName, string newValue, string editedBy, CancellationToken ct = default)
    {
        if (!AiFieldNames.All.Contains(fieldName))
            throw new ArgumentException($"Unknown AI field: {fieldName}");
        var aiFields = await EnsureAiFieldsAsync(candidateId, ct);
        ApplyToAiFields(aiFields, fieldName, newValue);
        await UpsertOverrideAsync(candidateId, fieldName, newValue, editedBy, isUserEdit: true, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task RevertFieldAsync(Guid candidateId, string fieldName, string revertedBy, CancellationToken ct = default)
    {
        var ov = await _db.CandidateAiFieldOverrides
            .FirstOrDefaultAsync(o => o.CandidateId == candidateId && o.FieldName == fieldName, ct)
            ?? throw new InvalidOperationException("No override found");
        if (ov.OriginalAiValue is null) throw new InvalidOperationException("No original AI value to revert to");
        var aiFields = await EnsureAiFieldsAsync(candidateId, ct);
        ApplyToAiFields(aiFields, fieldName, ov.OriginalAiValue);
        ov.CurrentValue = ov.OriginalAiValue;
        ov.IsUserEdited = false;
        ov.LastEditedBy = revertedBy;
        await _db.SaveChangesAsync(ct);
    }

    private async Task<Candidate?> LoadCandidateAsync(Guid id, CancellationToken ct) =>
        await _db.Candidates
            .Include(c => c.Skills)
            .Include(c => c.WorkExperiences)
            .Include(c => c.EducationEntries)
            .Include(c => c.Certifications)
            .Include(c => c.Projects)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

    private async Task<CandidateAiFields> EnsureAiFieldsAsync(Guid candidateId, CancellationToken ct)
    {
        var f = await _db.CandidateAiFields.FirstOrDefaultAsync(x => x.CandidateId == candidateId, ct);
        if (f is null)
        {
            f = new CandidateAiFields { CandidateId = candidateId };
            _db.CandidateAiFields.Add(f);
            await _db.SaveChangesAsync(ct);
        }
        return f;
    }

    private static void ApplyToAiFields(CandidateAiFields f, string fieldName, string value)
    {
        switch (fieldName)
        {
            case AiFieldNames.AiSummary: f.AiSummary = value; break;
            case AiFieldNames.AiSeniorityLevel: f.AiSeniorityLevel = value; break;
            case AiFieldNames.AiSeniorityRationale: f.AiSeniorityRationale = value; break;
            case AiFieldNames.AiTopStrengths: f.AiTopStrengths = value; break;
            case AiFieldNames.AiSkillCategories: f.AiSkillCategories = value; break;
            case AiFieldNames.AiYearsExperienceEstimate:
                if (decimal.TryParse(value, System.Globalization.NumberStyles.Number,
                        System.Globalization.CultureInfo.InvariantCulture, out var d))
                    f.AiYearsExperienceEstimate = d;
                break;
            case AiFieldNames.AiSuggestedRoles: f.AiSuggestedRoles = value; break;
            case AiFieldNames.AiInterviewFocusAreas: f.AiInterviewFocusAreas = value; break;
        }
    }
}
