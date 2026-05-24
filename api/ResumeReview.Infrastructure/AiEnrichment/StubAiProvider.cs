using System.Diagnostics;
using System.Text.Json;
using ResumeReview.Domain;

namespace ResumeReview.Infrastructure.AiEnrichment;

/// <summary>
/// Deterministic provider that produces plausible AI-style output without calling any service.
/// Used by default for local dev/tests; swap in <see cref="AzureFoundryAiProvider"/> when configured.
/// </summary>
public class StubAiProvider : IAiProvider
{
    public string ModelName => "stub-gpt-5.4-mini";

    public async Task<AiGenerationResult> GenerateAsync(
        string fieldName, Candidate candidate, string? extraInstructions, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        await Task.Delay(20, ct); // simulate latency
        var prompt = BuildPrompt(fieldName, candidate, extraInstructions);
        var value = fieldName switch
        {
            AiFieldNames.AiSummary => Summary(candidate, extraInstructions),
            AiFieldNames.AiSeniorityLevel => InferSeniority(candidate),
            AiFieldNames.AiSeniorityRationale => SeniorityRationale(candidate),
            AiFieldNames.AiTopStrengths => Strengths(candidate),
            AiFieldNames.AiSkillCategories => SkillCategories(candidate),
            AiFieldNames.AiYearsExperienceEstimate => YearsExperience(candidate).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture),
            AiFieldNames.AiSuggestedRoles => SuggestedRoles(candidate),
            AiFieldNames.AiInterviewFocusAreas => InterviewFocus(candidate),
            _ => "n/a"
        };
        sw.Stop();
        var usage = JsonSerializer.Serialize(new { promptTokens = prompt.Length / 4, completionTokens = value.Length / 4 });
        return new AiGenerationResult(value, prompt, ModelName, sw.ElapsedMilliseconds, usage);
    }

    private static string BuildPrompt(string field, Candidate c, string? extra)
        => $"Generate {field} for candidate {c.FullName} ({c.Email}). Skills: {string.Join(", ", c.Skills.Select(s => s.Name))}. "
         + $"Experiences: {c.WorkExperiences.Count}. Education: {c.EducationEntries.Count}. "
         + $"Extra: {extra ?? "none"}";

    private static string Summary(Candidate c, string? extra)
    {
        var topSkills = string.Join(", ", c.Skills.OrderBy(s => s.OrderIndex).Take(3).Select(s => s.Name));
        var years = YearsExperience(c).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
        var concise = extra?.Contains("concise", StringComparison.OrdinalIgnoreCase) == true;
        var basic = $"{c.FullName} is an ML practitioner with ~{years} years of experience, strongest in {topSkills}.";
        return concise ? basic : basic + " Background spans research, product, and platform work; would interview well for senior IC roles.";
    }

    public static decimal YearsExperience(Candidate c)
    {
        if (c.WorkExperiences.Count == 0) return 0m;
        decimal totalMonths = 0;
        foreach (var e in c.WorkExperiences)
        {
            var end = e.EndDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
            var months = ((end.Year - e.StartDate.Year) * 12) + (end.Month - e.StartDate.Month);
            if (months > 0) totalMonths += months;
        }
        return Math.Round(totalMonths / 12m, 1);
    }

    private static string InferSeniority(Candidate c)
    {
        var y = YearsExperience(c);
        return y switch
        {
            < 2m => "Junior",
            < 5m => "Mid",
            < 9m => "Senior",
            < 13m => "Staff",
            _ => "Principal"
        };
    }

    private static string SeniorityRationale(Candidate c)
        => $"~{YearsExperience(c):0.0} years across {c.WorkExperiences.Count} roles; skill breadth across {c.Skills.Count} technologies.";

    private static string Strengths(Candidate c)
    {
        var top = c.Skills.OrderBy(s => s.OrderIndex).Take(5).Select(s => s.Name).ToList();
        while (top.Count < 3) top.Add("Communication");
        return JsonSerializer.Serialize(top);
    }

    private static string SkillCategories(Candidate c)
    {
        var languages = new[] { "Python", "C++", "Java", "JavaScript", "TypeScript", "Go", "Rust", "Scala", "R" };
        var frameworks = new[] { "PyTorch", "TensorFlow", "JAX", "Hugging Face", "scikit-learn", "Ray", "FastAPI", "Spark" };
        var cloud = new[] { "AWS", "Azure", "GCP", "Kubernetes", "Docker", "Terraform" };
        var dbs = new[] { "PostgreSQL", "MySQL", "MongoDB", "Redis", "Snowflake", "BigQuery" };

        string[] BucketOf(IEnumerable<string> wanted) =>
            c.Skills.Select(s => s.Name).Intersect(wanted, StringComparer.OrdinalIgnoreCase).ToArray();

        var soft = new[] { "Mentorship", "Communication", "Cross-functional collaboration" };

        return JsonSerializer.Serialize(new
        {
            Languages = BucketOf(languages),
            Frameworks = BucketOf(frameworks),
            Cloud = BucketOf(cloud),
            Databases = BucketOf(dbs),
            SoftSkills = soft.Take(2).ToArray()
        });
    }

    private static string SuggestedRoles(Candidate c)
    {
        var y = YearsExperience(c);
        var roles = y switch
        {
            < 3m => new[] { "ML Engineer", "Applied Scientist", "Data Scientist" },
            < 7m => new[] { "Senior ML Engineer", "Applied Scientist II", "MLOps Lead" },
            _ => new[] { "Staff ML Engineer", "ML Tech Lead", "Principal Applied Scientist" }
        };
        return JsonSerializer.Serialize(roles);
    }

    private static string InterviewFocus(Candidate c)
    {
        var areas = new List<string> { "System design for ML serving", "Recent project deep-dive" };
        if (c.Skills.Any(s => s.Name.Contains("LLM", StringComparison.OrdinalIgnoreCase)))
            areas.Add("LLM evaluation methodology");
        if (c.WorkExperiences.Any(e => e.Description.Contains("research", StringComparison.OrdinalIgnoreCase)))
            areas.Add("Translating research into production");
        return JsonSerializer.Serialize(areas);
    }
}
