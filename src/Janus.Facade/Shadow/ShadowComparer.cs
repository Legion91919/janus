using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Janus.Facade.Routing;

namespace Janus.Facade.Shadow;

internal sealed record ShadowWork(string Capability, string Rest, CapabilityResult Legacy, string? CorrelationId);

/// <summary>Bounded hand-off from the request path to the background comparer. Drops the oldest work when full: shadowing must never slow real traffic.</summary>
internal sealed class ShadowQueue
{
    private readonly Channel<ShadowWork> _channel = Channel.CreateBounded<ShadowWork>(
        new BoundedChannelOptions(512) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true });

    public ChannelReader<ShadowWork> Reader => _channel.Reader;

    public bool TryEnqueue(ShadowWork work) => _channel.Writer.TryWrite(work);
}

/// <summary>Matched / mismatched counts per capability, plus the field paths of the last mismatch (no values).</summary>
internal sealed class ShadowReport
{
    private readonly ConcurrentDictionary<string, Counts> _counts = new(StringComparer.OrdinalIgnoreCase);

    public void Record(string capability, IReadOnlyList<string> differences)
    {
        var counts = _counts.GetOrAdd(capability, _ => new Counts());
        if (differences.Count == 0) Interlocked.Increment(ref counts.Matched);
        else
        {
            Interlocked.Increment(ref counts.Mismatched);
            counts.LastMismatchFields = [.. differences];
        }
    }

    public ShadowSnapshot Snapshot(string capability) =>
        _counts.TryGetValue(capability, out var c)
            ? new ShadowSnapshot(Volatile.Read(ref c.Matched), Volatile.Read(ref c.Mismatched), c.LastMismatchFields)
            : new ShadowSnapshot(0, 0, []);

    private sealed class Counts
    {
        public long Matched;
        public long Mismatched;
        public string[] LastMismatchFields = [];
    }
}

internal sealed record ShadowSnapshot(long Matched, long Mismatched, IReadOnlyList<string> LastMismatchFields);

/// <summary>
/// Background comparer for Shadow mode: asks the new side the same question the legacy side just answered and
/// records whether they agree. Logs field paths, a route template and a hashed fingerprint, never values or identifiers.
/// </summary>
internal sealed partial class ShadowComparer(ShadowQueue queue, ShadowReport report, IServiceScopeFactory scopes, ILogger<ShadowComparer> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static string Fingerprint(string rest) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rest)))[..8].ToLowerInvariant();

    internal static string Canonical(CapabilityResult r) => r.Error is null
        ? JsonSerializer.Serialize(new { status = r.StatusCode, body = r.Body }, Json)
        : JsonSerializer.Serialize(new { status = r.StatusCode, code = r.Error.Code }, Json);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var work in queue.Reader.ReadAllAsync(stoppingToken).ConfigureAwait(false))
        {
            try
            {
                await CompareAsync(work, stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogCompareFailed(logger, work.Capability, ex.GetType().Name, work.CorrelationId);
            }
        }
    }

    internal async Task<IReadOnlyList<string>> CompareAsync(ShadowWork work, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var newSide = scope.ServiceProvider.GetServices<INewCapability>().Single(c => c.Name == work.Capability);
        var fresh = await newSide.GetAsync(work.Rest, ct).ConfigureAwait(false);
        var differences = JsonDiff.Paths(Canonical(work.Legacy), Canonical(fresh))
            .Select(p => p.Replace("$.body", "$", StringComparison.Ordinal)).ToList();
        if (differences.Count > 0)
        {
            var route = work.Rest.Length == 0 ? "/" : "/{id}";
            LogMismatch(logger, work.Capability, route, Fingerprint(work.Rest), work.CorrelationId, string.Join(",", differences));
        }

        report.Record(work.Capability, differences);

        return differences;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Shadow mismatch for {Capability} {Route} fingerprint {Fingerprint} correlation {CorrelationId}: {Fields}")]
    private static partial void LogMismatch(ILogger logger, string capability, string route, string fingerprint, string? correlationId, string fields);

    [LoggerMessage(Level = LogLevel.Error, Message = "Shadow comparison failed for {Capability} ({ErrorType}) correlation {CorrelationId}")]
    private static partial void LogCompareFailed(ILogger logger, string capability, string errorType, string? correlationId);
}
