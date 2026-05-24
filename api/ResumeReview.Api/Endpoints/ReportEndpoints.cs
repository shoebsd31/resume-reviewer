using Microsoft.EntityFrameworkCore;
using ResumeReview.Api.Dto;
using ResumeReview.Domain;
using ResumeReview.Infrastructure.Persistence;

namespace ResumeReview.Api.Endpoints;

public static class ReportEndpoints
{
    public static IEndpointRouteBuilder MapReportEndpoints(this IEndpointRouteBuilder app)
    {
        var grp = app.MapGroup("/api/report").WithTags("Report");

        grp.MapGet("/", async (ResumeReviewDbContext db) =>
        {
            var reviewed = await db.Candidates
                .Where(c => c.ReviewStatus == ReviewStatus.Reviewed)
                .Include(c => c.Skills)
                .Include(c => c.WorkExperiences)
                .Include(c => c.EducationEntries)
                .Include(c => c.Certifications)
                .Include(c => c.Projects)
                .Include(c => c.AiFields)
                .AsNoTracking()
                .ToListAsync();
            return Results.Ok(reviewed.Select(CandidateDto.Detail));
        });

        return app;
    }
}
