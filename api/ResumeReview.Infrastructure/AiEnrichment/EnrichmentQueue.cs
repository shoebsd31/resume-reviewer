using System.Threading.Channels;

namespace ResumeReview.Infrastructure.AiEnrichment;

public interface IEnrichmentQueue
{
    ValueTask EnqueueAsync(EnrichmentJob job, CancellationToken ct = default);
    IAsyncEnumerable<EnrichmentJob> ReadAllAsync(CancellationToken ct = default);
}

public class ChannelEnrichmentQueue : IEnrichmentQueue
{
    private readonly Channel<EnrichmentJob> _channel = Channel.CreateUnbounded<EnrichmentJob>(
        new UnboundedChannelOptions { SingleReader = false, SingleWriter = false });

    public ValueTask EnqueueAsync(EnrichmentJob job, CancellationToken ct = default)
        => _channel.Writer.WriteAsync(job, ct);

    public IAsyncEnumerable<EnrichmentJob> ReadAllAsync(CancellationToken ct = default)
        => _channel.Reader.ReadAllAsync(ct);
}
