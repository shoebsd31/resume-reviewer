using System.Diagnostics;
using System.Text.Json;
using Azure;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using ResumeReview.Domain;

namespace ResumeReview.Infrastructure.AiEnrichment;

public class AzureAiOptions
{
    public string? Endpoint { get; set; }
    public string? ApiKey { get; set; }
    public string DeploymentName { get; set; } = "gpt-5.4-mini";
    public string ApiVersion { get; set; } = "2024-12-01-preview";
    public bool UseStub { get; set; } = true;
}

public class AzureFoundryAiProvider : IAiProvider
{
    private readonly AzureAiOptions _opts;
    private readonly Lazy<ChatClient> _client;

    public string ModelName => _opts.DeploymentName;

    public AzureFoundryAiProvider(IOptions<AzureAiOptions> opts)
    {
        _opts = opts.Value;
        _client = new Lazy<ChatClient>(() =>
        {
            var azure = new AzureOpenAIClient(new Uri(_opts.Endpoint!), new AzureKeyCredential(_opts.ApiKey!));
            return azure.GetChatClient(_opts.DeploymentName);
        });
    }

    public async Task<AiGenerationResult> GenerateAsync(
        string fieldName, Candidate candidate, string? extraInstructions, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var system = "You are an expert technical recruiter. Respond with ONLY the requested field value — no preamble.";
        var prompt = BuildPrompt(fieldName, candidate, extraInstructions);
        var messages = new ChatMessage[]
        {
            new SystemChatMessage(system),
            new UserChatMessage(prompt)
        };
        var response = await _client.Value.CompleteChatAsync(messages, cancellationToken: ct);
        sw.Stop();
        var text = response.Value.Content.Count > 0 ? response.Value.Content[0].Text : string.Empty;
        var usage = response.Value.Usage is null ? null : JsonSerializer.Serialize(new
        {
            promptTokens = response.Value.Usage.InputTokenCount,
            completionTokens = response.Value.Usage.OutputTokenCount,
            totalTokens = response.Value.Usage.TotalTokenCount
        });
        return new AiGenerationResult(text.Trim(), prompt, ModelName, sw.ElapsedMilliseconds, usage);
    }

    private static string BuildPrompt(string field, Candidate c, string? extra)
    {
        var skills = string.Join(", ", c.Skills.Select(s => s.Name));
        var roles = string.Join("; ", c.WorkExperiences.Select(e => $"{e.Title} at {e.Company} ({e.StartDate:yyyy-MM}–{(e.EndDate?.ToString("yyyy-MM") ?? "Present")})"));
        var edu = string.Join("; ", c.EducationEntries.Select(e => $"{e.Degree} {e.Field} @ {e.Institution} ({e.GraduationYear})"));
        var instruction = field switch
        {
            AiFieldNames.AiSummary => "Write a 2-3 sentence elevator-pitch summary.",
            AiFieldNames.AiSeniorityLevel => "Return EXACTLY one of: Junior, Mid, Senior, Staff, Principal.",
            AiFieldNames.AiSeniorityRationale => "Explain the seniority assessment in 1-2 sentences.",
            AiFieldNames.AiTopStrengths => "Return a JSON array of 3-5 short strength phrases.",
            AiFieldNames.AiSkillCategories => "Return a JSON object with keys Languages, Frameworks, Cloud, Databases, SoftSkills (arrays of strings).",
            AiFieldNames.AiYearsExperienceEstimate => "Return a single decimal number, e.g. 6.5",
            AiFieldNames.AiSuggestedRoles => "Return a JSON array of 3 suitable role titles.",
            AiFieldNames.AiInterviewFocusAreas => "Return a JSON array of 3-5 interview focus areas.",
            _ => "Generate the field."
        };
        return $"Candidate: {c.FullName}\nSummary: {c.Summary}\nSkills: {skills}\nRoles: {roles}\nEducation: {edu}\n\nField: {field}\n{instruction}\nExtra instructions: {extra ?? "(none)"}";
    }
}
