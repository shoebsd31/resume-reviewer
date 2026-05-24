using ResumeReview.Domain;

namespace ResumeReview.Api.Dto;

public record SkillDto(Guid Id, string Name, int OrderIndex);
public record WorkExperienceDto(Guid Id, string Title, string Company, string StartDate, string? EndDate, string Description, int OrderIndex);
public record EducationDto(Guid Id, string Institution, string Degree, string Field, int? GraduationYear, int OrderIndex);
public record CertificationDto(Guid Id, string Name, string Issuer, int? Year, int OrderIndex);
public record ProjectDto(Guid Id, string Name, string Description, string TechStack, int OrderIndex);

public record AiFieldsDto(
    string? AiSummary, string? AiSeniorityLevel, string? AiSeniorityRationale,
    string? AiTopStrengths, string? AiSkillCategories, decimal? AiYearsExperienceEstimate,
    string? AiSuggestedRoles, string? AiInterviewFocusAreas,
    DateTime? LastEnrichedAt, string EnrichmentStatus, string? LastError);

public record AiOverrideDto(string FieldName, string? OriginalAiValue, string? CurrentValue, bool IsUserEdited, DateTime UpdatedAt);

public record CandidateSummaryDto(
    Guid Id, string FullName, string Email, string? Location,
    string ReviewStatus, string? AiSeniorityLevel, decimal? AiYearsExperienceEstimate,
    string? TopSkill, string? AiSummary, IReadOnlyList<string> Skills,
    DateTime UpdatedAt);

public record CandidateDetailDto(
    Guid Id, string FullName, string Email, string? Phone, string? Location,
    string? LinkedInUrl, string? GitHubUrl, string Summary, string SourceFileName,
    string ReviewStatus, DateTime CreatedAt, DateTime UpdatedAt, string LastEditedBy,
    IReadOnlyList<SkillDto> Skills,
    IReadOnlyList<WorkExperienceDto> WorkExperiences,
    IReadOnlyList<EducationDto> Education,
    IReadOnlyList<CertificationDto> Certifications,
    IReadOnlyList<ProjectDto> Projects,
    AiFieldsDto? AiFields,
    IReadOnlyList<AiOverrideDto> AiOverrides);

public static class CandidateDto
{
    public static CandidateSummaryDto Summary(Candidate c) => new(
        c.Id, c.FullName, c.Email, c.Location,
        c.ReviewStatus.ToString(),
        c.AiFields?.AiSeniorityLevel,
        c.AiFields?.AiYearsExperienceEstimate,
        c.Skills.OrderBy(s => s.OrderIndex).FirstOrDefault()?.Name,
        c.AiFields?.AiSummary,
        c.Skills.OrderBy(s => s.OrderIndex).Select(s => s.Name).ToList(),
        c.UpdatedAt);

    public static CandidateDetailDto Detail(Candidate c) => new(
        c.Id, c.FullName, c.Email, c.Phone, c.Location, c.LinkedInUrl, c.GitHubUrl,
        c.Summary, c.SourceFileName, c.ReviewStatus.ToString(),
        c.CreatedAt, c.UpdatedAt, c.LastEditedBy,
        c.Skills.OrderBy(s => s.OrderIndex)
            .Select(s => new SkillDto(s.Id, s.Name, s.OrderIndex)).ToList(),
        c.WorkExperiences.OrderBy(w => w.OrderIndex)
            .Select(w => new WorkExperienceDto(w.Id, w.Title, w.Company,
                w.StartDate.ToString("yyyy-MM-dd"),
                w.EndDate?.ToString("yyyy-MM-dd"),
                w.Description, w.OrderIndex)).ToList(),
        c.EducationEntries.OrderBy(e => e.OrderIndex)
            .Select(e => new EducationDto(e.Id, e.Institution, e.Degree, e.Field, e.GraduationYear, e.OrderIndex)).ToList(),
        c.Certifications.OrderBy(x => x.OrderIndex)
            .Select(x => new CertificationDto(x.Id, x.Name, x.Issuer, x.Year, x.OrderIndex)).ToList(),
        c.Projects.OrderBy(p => p.OrderIndex)
            .Select(p => new ProjectDto(p.Id, p.Name, p.Description, p.TechStack, p.OrderIndex)).ToList(),
        c.AiFields is null ? null : new AiFieldsDto(
            c.AiFields.AiSummary, c.AiFields.AiSeniorityLevel, c.AiFields.AiSeniorityRationale,
            c.AiFields.AiTopStrengths, c.AiFields.AiSkillCategories, c.AiFields.AiYearsExperienceEstimate,
            c.AiFields.AiSuggestedRoles, c.AiFields.AiInterviewFocusAreas,
            c.AiFields.LastEnrichedAt, c.AiFields.EnrichmentStatus.ToString(), c.AiFields.LastError),
        c.AiOverrides.Select(o => new AiOverrideDto(
            o.FieldName, o.OriginalAiValue, o.CurrentValue, o.IsUserEdited, o.UpdatedAt)).ToList());
}
