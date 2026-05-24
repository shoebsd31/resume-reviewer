namespace ResumeReview.Infrastructure.Ingestion;

public record IngestionResult(Guid IngestionId, Guid CandidateId, bool IsNew, string? Error = null)
{
    public bool Success => Error is null;
}
