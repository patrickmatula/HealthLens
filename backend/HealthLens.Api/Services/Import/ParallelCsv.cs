using System.Threading.Channels;

namespace HealthLens.Api.Services.Import;

/// <summary>
/// Fans a set of export files out across all cores for parsing and funnels the results into a single
/// writer. SQLite allows exactly one writer at a time (and in ephemeral mode every context shares the
/// same in-memory database), so parsing — which is where an import actually spends its time — is what
/// gets parallelised, while the inserts stay on one thread behind a bounded channel that also caps how
/// much parsed data is held in memory at once.
/// </summary>
public static class ParallelCsv
{
    public static int Degree { get; } = Math.Clamp(Environment.ProcessorCount, 2, 16);

    public static async Task RunAsync<TItem, TResult>(
        IReadOnlyList<TItem> items,
        Func<TItem, TResult> parse,
        Action<TResult> consume,
        Action<int>? onProgress,
        CancellationToken ct)
    {
        if (items.Count == 0)
        {
            return;
        }

        var channel = Channel.CreateBounded<TResult>(new BoundedChannelOptions(Degree * 2)
        {
            SingleReader = true,
            FullMode = BoundedChannelFullMode.Wait,
        });

        var writerTask = Task.Run(
            async () =>
            {
                var written = 0;
                await foreach (var parsed in channel.Reader.ReadAllAsync(ct))
                {
                    consume(parsed);
                    onProgress?.Invoke(++written);
                }
            },
            ct);

        try
        {
            await Parallel.ForEachAsync(
                items,
                new ParallelOptions { MaxDegreeOfParallelism = Degree, CancellationToken = ct },
                async (item, token) => await channel.Writer.WriteAsync(parse(item), token));
            channel.Writer.Complete();
        }
        catch (Exception ex)
        {
            channel.Writer.TryComplete(ex);
        }

        await writerTask;
    }
}
