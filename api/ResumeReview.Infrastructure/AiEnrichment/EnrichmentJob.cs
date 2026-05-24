namespace ResumeReview.Infrastructure.AiEnrichment;

public record EnrichmentJob(
    Guid CandidateId,
    string? SingleFieldName = null,
    string? ExtraInstructions = null,
    string RequestedBy = "system");
