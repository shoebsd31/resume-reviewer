using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ResumeReview.Infrastructure;
using ResumeReview.Infrastructure.AiEnrichment;
using ResumeReview.Infrastructure.Ingestion;
using ResumeReview.Infrastructure.Persistence;

namespace ResumeReview.Api;

public static class SeedRunner
{
    public static async Task RunAsync(string[] args)
    {
        var path = "./samples-resumes/output";
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i].Equals("--path", StringComparison.OrdinalIgnoreCase))
                path = args[i + 1];
        }

        var builder = Host.CreateApplicationBuilder(args);
        builder.Configuration.AddEnvironmentVariables();
        builder.Services.AddResumeReviewInfrastructure(builder.Configuration);
        builder.Services.AddLogging(l => l.AddSimpleConsole());
        var host = builder.Build();

        using var scope = host.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("seed");
        var db = sp.GetRequiredService<ResumeReviewDbContext>();
        await db.Database.EnsureCreatedAsync();

        var ingestion = sp.GetRequiredService<IIngestionService>();
        var enrichment = sp.GetRequiredService<IEnrichmentService>();

        if (!Directory.Exists(path))
        {
            logger.LogError("Seed path does not exist: {Path}", path);
            return;
        }

        var files = Directory.EnumerateFiles(path, "*.docx").ToList();
        logger.LogInformation("Seeding {Count} files from {Path}", files.Count, path);
        var ids = new List<Guid>();
        foreach (var file in files)
        {
            await using var stream = File.OpenRead(file);
            var result = await ingestion.IngestAsync(stream, Path.GetFileName(file));
            if (result.Success)
            {
                ids.Add(result.CandidateId);
                logger.LogInformation("  ingested {File} → {Id} (new={IsNew})",
                    Path.GetFileName(file), result.CandidateId, result.IsNew);
            }
            else
            {
                logger.LogWarning("  FAILED {File}: {Error}", Path.GetFileName(file), result.Error);
            }
        }

        // Eagerly enrich for the seed command (so the user can see fully-populated rows immediately).
        foreach (var id in ids)
        {
            try { await enrichment.EnrichCandidateAsync(id); }
            catch (Exception ex) { logger.LogWarning(ex, "Enrichment failed for {Id}", id); }
        }
        logger.LogInformation("Seed complete: {Count} candidate(s) enriched.", ids.Count);
    }
}
