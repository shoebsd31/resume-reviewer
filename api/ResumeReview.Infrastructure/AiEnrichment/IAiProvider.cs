using ResumeReview.Domain;

namespace ResumeReview.Infrastructure.AiEnrichment;

public record AiGenerationResult(
    string Value,
    string Prompt,
    string ModelName,
    long LatencyMs,
    string? TokenUsage);

public interface IAiProvider
{
    Task<AiGenerationResult> GenerateAsync(
        string fieldName,
        Candidate candidate,
        string? extraInstructions,
        CancellationToken ct = default);

    string ModelName { get; }
}
