namespace ResumeReview.Domain;

public enum ReviewStatus
{
    Pending = 0,
    Reviewed = 1,
    Rejected = 2
}

public enum EnrichmentStatus
{
    Pending = 0,
    InProgress = 1,
    Completed = 2,
    Failed = 3
}

public enum GenerationStatus
{
    Success = 0,
    Failure = 1
}
