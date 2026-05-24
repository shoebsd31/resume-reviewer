using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ResumeReview.Infrastructure.AiEnrichment;
using ResumeReview.Infrastructure.Ingestion;
using ResumeReview.Infrastructure.Parsing;
using ResumeReview.Infrastructure.Persistence;

namespace ResumeReview.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddResumeReviewInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddSingleton<AuditInterceptor>();

        var connectionString = config.GetConnectionString("ResumeReviewDb")
                               ?? config["ConnectionStrings:ResumeReviewDb"]
                               ?? config["ConnectionStrings__ResumeReviewDb"];
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Missing configuration: ConnectionStrings__ResumeReviewDb");

        services.AddDbContext<ResumeReviewDbContext>((sp, options) =>
        {
            var isSqlite = connectionString.Contains("DataSource=", StringComparison.OrdinalIgnoreCase)
                        || connectionString.Contains("Data Source=:memory:", StringComparison.OrdinalIgnoreCase)
                        || connectionString.StartsWith("Filename=", StringComparison.OrdinalIgnoreCase);
            if (isSqlite) options.UseSqlite(connectionString);
            else options.UseSqlServer(connectionString);
            options.AddInterceptors(sp.GetRequiredService<AuditInterceptor>());
        });

        services.Configure<AzureAiOptions>(config.GetSection("AzureAi"));
        services.Configure<WorkerOptions>(config.GetSection("Worker"));

        services.AddSingleton<IEnrichmentQueue, ChannelEnrichmentQueue>();
        services.AddScoped<IResumeDocumentParser, ResumeDocumentParser>();
        services.AddScoped<IIngestionService, IngestionService>();
        services.AddScoped<IEnrichmentService, EnrichmentService>();
        services.AddScoped<EnrichmentService>(sp => (EnrichmentService)sp.GetRequiredService<IEnrichmentService>());

        services.AddScoped<IAiProvider>(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<AzureAiOptions>>().Value;
            if (opts.UseStub || string.IsNullOrWhiteSpace(opts.Endpoint) || string.IsNullOrWhiteSpace(opts.ApiKey))
                return new StubAiProvider();
            return new AzureFoundryAiProvider(Options.Create(opts));
        });

        return services;
    }
}
