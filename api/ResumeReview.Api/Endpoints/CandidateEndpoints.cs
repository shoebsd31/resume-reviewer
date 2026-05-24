using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ResumeReview.Api.Dto;
using ResumeReview.Domain;
using ResumeReview.Infrastructure.AiEnrichment;
using ResumeReview.Infrastructure.Persistence;

namespace ResumeReview.Api.Endpoints;

public static class CandidateEndpoints
{
    public static IEndpointRouteBuilder MapCandidateEndpoints(this IEndpointRouteBuilder app)
    {
        var grp = app.MapGroup("/api/candidates").WithTags("Candidates");

        grp.MapGet("/", async (ResumeReviewDbContext db) =>
        {
            var rows = await db.Candidates
                .Include(c => c.AiFields)
                .Include(c => c.Skills)
                .AsNoTracking()
                .ToListAsync();
            return Results.Ok(rows.Select(CandidateDto.Summary));
        });

        grp.MapGet("/{id:guid}", async (Guid id, ResumeReviewDbContext db) =>
        {
            var c = await db.Candidates
                .Include(x => x.Skills)
                .Include(x => x.WorkExperiences)
                .Include(x => x.EducationEntries)
                .Include(x => x.Certifications)
                .Include(x => x.Projects)
                .Include(x => x.AiFields)
                .Include(x => x.AiOverrides)
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id);
            return c is null ? Results.NotFound() : Results.Ok(CandidateDto.Detail(c));
        });

        grp.MapPost("/{id:guid}/review", async (Guid id, [FromBody] ReviewUpdateRequest req, ResumeReviewDbContext db) =>
        {
            var c = await db.Candidates.FirstOrDefaultAsync(x => x.Id == id);
            if (c is null) return Results.NotFound();
            if (!Enum.TryParse<ReviewStatus>(req.Status, true, out var status))
                return Results.BadRequest(new { error = "invalid_status" });
            c.ReviewStatus = status;
            c.LastEditedBy = req.UpdatedBy ?? "user";
            await db.SaveChangesAsync();
            return Results.Ok(new { id = c.Id, status = c.ReviewStatus.ToString() });
        });

        grp.MapPost("/{id:guid}/ai-fields/{fieldName}/regenerate",
            async (Guid id, string fieldName, [FromBody] RegenerateRequest req,
                   IEnrichmentService svc) =>
            {
                var result = await svc.RegenerateFieldAsync(id, fieldName, req.ExtraInstructions, req.RequestedBy ?? "user");
                return Results.Ok(new { newValue = result.NewValue, historyId = result.HistoryId });
            });

        grp.MapPost("/{id:guid}/ai-fields/regenerate-all",
            async (Guid id, IEnrichmentService svc) =>
            {
                await svc.EnrichCandidateAsync(id, "user");
                return Results.Ok(new { id, status = "completed" });
            });

        grp.MapPost("/{id:guid}/ai-fields/{fieldName}/edit",
            async (Guid id, string fieldName, [FromBody] EditAiFieldRequest req, IEnrichmentService svc) =>
            {
                if (svc is EnrichmentService es)
                {
                    await es.ApplyUserEditAsync(id, fieldName, req.Value, req.UpdatedBy ?? "user");
                    return Results.Ok();
                }
                return Results.Problem("Edit not supported by this provider");
            });

        grp.MapPost("/{id:guid}/ai-fields/{fieldName}/revert",
            async (Guid id, string fieldName, IEnrichmentService svc) =>
            {
                if (svc is EnrichmentService es)
                {
                    await es.RevertFieldAsync(id, fieldName, "user");
                    return Results.Ok();
                }
                return Results.Problem("Revert not supported by this provider");
            });

        grp.MapGet("/{id:guid}/ai-fields/{fieldName}/history",
            async (Guid id, string fieldName, ResumeReviewDbContext db) =>
            {
                var rows = await db.AiGenerationHistory
                    .Where(h => h.CandidateId == id && h.FieldName == fieldName)
                    .OrderByDescending(h => h.RequestedAt)
                    .AsNoTracking()
                    .ToListAsync();
                return Results.Ok(rows.Select(r => new
                {
                    r.Id, r.FieldName, r.ModelName, r.PromptText, r.ExtraInstructions,
                    r.ResponseText, r.LatencyMs, r.TokenUsage,
                    r.RequestedBy, r.RequestedAt, status = r.Status.ToString(), r.ErrorMessage
                }));
            });

        return app;
    }
}

public record ReviewUpdateRequest(string Status, string? UpdatedBy);
public record RegenerateRequest(string? ExtraInstructions, string? RequestedBy);
public record EditAiFieldRequest(string Value, string? UpdatedBy);
