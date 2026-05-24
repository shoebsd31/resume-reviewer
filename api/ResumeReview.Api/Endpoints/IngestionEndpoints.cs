using ResumeReview.Infrastructure.Ingestion;

namespace ResumeReview.Api.Endpoints;

public static class IngestionEndpoints
{
    public static IEndpointRouteBuilder MapIngestionEndpoints(this IEndpointRouteBuilder app)
    {
        var grp = app.MapGroup("/api/resumes").WithTags("Resumes");

        grp.MapPost("/upload", async (HttpRequest request, IIngestionService ingestion) =>
        {
            if (!request.HasFormContentType)
                return Results.BadRequest(new { error = "multipart_required" });
            var form = await request.ReadFormAsync();
            if (form.Files.Count == 0)
                return Results.BadRequest(new { error = "no_files" });

            var results = new List<object>();
            foreach (var file in form.Files)
            {
                await using var stream = file.OpenReadStream();
                var result = await ingestion.IngestAsync(stream, file.FileName);
                results.Add(new
                {
                    fileName = file.FileName,
                    ingestionId = result.IngestionId,
                    candidateId = result.CandidateId == Guid.Empty ? (Guid?)null : result.CandidateId,
                    isNew = result.IsNew,
                    success = result.Success,
                    error = result.Error
                });
            }

            return Results.Accepted(value: results);
        }).DisableAntiforgery();

        return app;
    }
}
