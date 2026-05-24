using Microsoft.EntityFrameworkCore;
using ResumeReview.Domain;

namespace ResumeReview.Infrastructure.Persistence;

public class ResumeReviewDbContext : DbContext
{
    public ResumeReviewDbContext(DbContextOptions<ResumeReviewDbContext> options) : base(options) { }

    public DbSet<Candidate> Candidates => Set<Candidate>();
    public DbSet<Skill> Skills => Set<Skill>();
    public DbSet<WorkExperience> WorkExperiences => Set<WorkExperience>();
    public DbSet<Education> EducationEntries => Set<Education>();
    public DbSet<Certification> Certifications => Set<Certification>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<CandidateAiFields> CandidateAiFields => Set<CandidateAiFields>();
    public DbSet<CandidateAiFieldOverride> CandidateAiFieldOverrides => Set<CandidateAiFieldOverride>();
    public DbSet<AiGenerationHistory> AiGenerationHistory => Set<AiGenerationHistory>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Candidate>(e =>
        {
            e.HasIndex(c => c.Email);
            e.HasIndex(c => new { c.FullName, c.Email }).IsUnique();
            e.Property(c => c.ReviewStatus).HasConversion<int>();
        });

        b.Entity<CandidateAiFields>(e =>
        {
            e.HasKey(x => x.CandidateId);
            e.Property(x => x.EnrichmentStatus).HasConversion<int>();
            e.HasOne(x => x.Candidate)
             .WithOne(c => c.AiFields)
             .HasForeignKey<CandidateAiFields>(x => x.CandidateId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        foreach (var rel in new[]
        {
            (typeof(Skill), "Skills"),
            (typeof(WorkExperience), "WorkExperiences"),
            (typeof(Education), "EducationEntries"),
            (typeof(Certification), "Certifications"),
            (typeof(Project), "Projects"),
            (typeof(CandidateAiFieldOverride), "AiOverrides"),
            (typeof(AiGenerationHistory), "AiHistory"),
        })
        {
            b.Entity(rel.Item1)
                .HasOne(typeof(Candidate), "Candidate")
                .WithMany(rel.Item2)
                .HasForeignKey("CandidateId")
                .OnDelete(DeleteBehavior.Cascade);
        }

        b.Entity<AiGenerationHistory>(e => e.Property(x => x.Status).HasConversion<int>());

        b.Entity<CandidateAiFieldOverride>(e =>
        {
            e.HasIndex(x => new { x.CandidateId, x.FieldName }).IsUnique();
        });

        // Provider-agnostic: convert DateOnly via string for SQLite test runs.
        if (Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true)
        {
            var conv = new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateOnly, string>(
                d => d.ToString("yyyy-MM-dd"),
                s => DateOnly.ParseExact(s, "yyyy-MM-dd"));
            var nullableConv = new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateOnly?, string?>(
                d => d == null ? null : d.Value.ToString("yyyy-MM-dd"),
                s => s == null ? null : DateOnly.ParseExact(s, "yyyy-MM-dd"));

            b.Entity<WorkExperience>().Property(x => x.StartDate).HasConversion(conv);
            b.Entity<WorkExperience>().Property(x => x.EndDate).HasConversion(nullableConv);
        }

        base.OnModelCreating(b);
    }
}
