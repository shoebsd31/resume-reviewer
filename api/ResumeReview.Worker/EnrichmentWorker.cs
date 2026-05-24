using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ResumeReview.Infrastructure.AiEnrichment;
using ResumeReview.Infrastructure.Persistence;

namespace ResumeReview.Worker;

public class EnrichmentWorker : BackgroundService
{
    private readonly IEnrichmentQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EnrichmentWorker> _logger;
    private readonly WorkerOptions _opts;

    public EnrichmentWorker(
        IEnrichmentQueue queue,
        IServiceScopeFactory scopeFactory,
        IOptions<WorkerOptions> opts,
        ILogger<EnrichmentWorker> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
        _opts = opts.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("EnrichmentWorker started (maxConcurrency={Max})", _opts.MaxConcurrentEnrichments);
        var sem = new SemaphoreSlim(_opts.MaxConcurrentEnrichments);
        var tasks = new List<Task>();

        await foreach (var job in _queue.ReadAllAsync(stoppingToken))
        {
            await sem.WaitAsync(stoppingToken);
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var svc = scope.ServiceProvider.GetRequiredService<IEnrichmentService>();
                    if (!string.IsNullOrEmpty(job.SingleFieldName))
                    {
                        await svc.RegenerateFieldAsync(job.CandidateId, job.SingleFieldName!, job.ExtraInstructions, job.RequestedBy, stoppingToken);
                    }
                    else
                    {
                        await svc.EnrichCandidateAsync(job.CandidateId, job.RequestedBy, stoppingToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Enrichment failed for candidate {Id}", job.CandidateId);
                }
                finally
                {
                    sem.Release();
                }
            }, stoppingToken));

            tasks.RemoveAll(t => t.IsCompleted);
        }

        await Task.WhenAll(tasks);
    }
}
