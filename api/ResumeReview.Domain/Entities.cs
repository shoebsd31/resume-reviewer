using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ResumeReview.Domain;

public abstract class AuditableEntity
{
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    [MaxLength(200)]
    public string LastEditedBy { get; set; } = "system";
}

public class Candidate : AuditableEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [MaxLength(200)]
    public string FullName { get; set; } = string.Empty;

    [MaxLength(320)]
    public string Email { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? Phone { get; set; }

    [MaxLength(200)]
    public string? Location { get; set; }

    [MaxLength(500)]
    public string? LinkedInUrl { get; set; }

    [MaxLength(500)]
    public string? GitHubUrl { get; set; }

    public string Summary { get; set; } = string.Empty;

    [MaxLength(500)]
    public string SourceFileName { get; set; } = string.Empty;

    public ReviewStatus ReviewStatus { get; set; } = ReviewStatus.Pending;

    public List<Skill> Skills { get; set; } = new();
    public List<WorkExperience> WorkExperiences { get; set; } = new();
    public List<Education> EducationEntries { get; set; } = new();
    public List<Certification> Certifications { get; set; } = new();
    public List<Project> Projects { get; set; } = new();

    public CandidateAiFields? AiFields { get; set; }
    public List<CandidateAiFieldOverride> AiOverrides { get; set; } = new();
    public List<AiGenerationHistory> AiHistory { get; set; } = new();
}

public class Skill : AuditableEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CandidateId { get; set; }
    [MaxLength(120)] public string Name { get; set; } = string.Empty;
    public int OrderIndex { get; set; }
    public Candidate? Candidate { get; set; }
}

public class WorkExperience : AuditableEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CandidateId { get; set; }
    [MaxLength(200)] public string Company { get; set; } = string.Empty;
    [MaxLength(200)] public string Title { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public int OrderIndex { get; set; }
    public Candidate? Candidate { get; set; }
}

public class Education : AuditableEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CandidateId { get; set; }
    [MaxLength(200)] public string Institution { get; set; } = string.Empty;
    [MaxLength(200)] public string Degree { get; set; } = string.Empty;
    [MaxLength(200)] public string Field { get; set; } = string.Empty;
    public int? GraduationYear { get; set; }
    public int OrderIndex { get; set; }
    public Candidate? Candidate { get; set; }
}

public class Certification : AuditableEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CandidateId { get; set; }
    [MaxLength(200)] public string Name { get; set; } = string.Empty;
    [MaxLength(200)] public string Issuer { get; set; } = string.Empty;
    public int? Year { get; set; }
    public int OrderIndex { get; set; }
    public Candidate? Candidate { get; set; }
}

public class Project : AuditableEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CandidateId { get; set; }
    [MaxLength(200)] public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    [MaxLength(500)] public string TechStack { get; set; } = string.Empty;
    public int OrderIndex { get; set; }
    public Candidate? Candidate { get; set; }
}

public class CandidateAiFields : AuditableEntity
{
    public Guid CandidateId { get; set; }
    public Candidate? Candidate { get; set; }

    public string? AiSummary { get; set; }
    [MaxLength(50)] public string? AiSeniorityLevel { get; set; }
    public string? AiSeniorityRationale { get; set; }
    public string? AiTopStrengths { get; set; }
    public string? AiSkillCategories { get; set; }
    [Column(TypeName = "decimal(4,1)")]
    public decimal? AiYearsExperienceEstimate { get; set; }
    public string? AiSuggestedRoles { get; set; }
    public string? AiInterviewFocusAreas { get; set; }
    public DateTime? LastEnrichedAt { get; set; }
    public EnrichmentStatus EnrichmentStatus { get; set; } = EnrichmentStatus.Pending;
    public string? LastError { get; set; }
}

public class CandidateAiFieldOverride : AuditableEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CandidateId { get; set; }
    public Candidate? Candidate { get; set; }
    [MaxLength(80)] public string FieldName { get; set; } = string.Empty;
    public string? OriginalAiValue { get; set; }
    public string? CurrentValue { get; set; }
    public bool IsUserEdited { get; set; }
}

public class AiGenerationHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CandidateId { get; set; }
    public Candidate? Candidate { get; set; }
    [MaxLength(80)] public string FieldName { get; set; } = string.Empty;
    [MaxLength(120)] public string ModelName { get; set; } = string.Empty;
    public string PromptText { get; set; } = string.Empty;
    public string? ExtraInstructions { get; set; }
    public string ResponseText { get; set; } = string.Empty;
    public long LatencyMs { get; set; }
    public string? TokenUsage { get; set; }
    [MaxLength(200)] public string RequestedBy { get; set; } = "system";
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public GenerationStatus Status { get; set; } = GenerationStatus.Success;
    public string? ErrorMessage { get; set; }
}
