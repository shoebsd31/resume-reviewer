namespace ResumeReview.Infrastructure.AiEnrichment;

public static class AiFieldNames
{
    public const string AiSummary = "AiSummary";
    public const string AiSeniorityLevel = "AiSeniorityLevel";
    public const string AiSeniorityRationale = "AiSeniorityRationale";
    public const string AiTopStrengths = "AiTopStrengths";
    public const string AiSkillCategories = "AiSkillCategories";
    public const string AiYearsExperienceEstimate = "AiYearsExperienceEstimate";
    public const string AiSuggestedRoles = "AiSuggestedRoles";
    public const string AiInterviewFocusAreas = "AiInterviewFocusAreas";

    public static readonly string[] All =
    {
        AiSummary,
        AiSeniorityLevel,
        AiSeniorityRationale,
        AiTopStrengths,
        AiSkillCategories,
        AiYearsExperienceEstimate,
        AiSuggestedRoles,
        AiInterviewFocusAreas
    };
}
