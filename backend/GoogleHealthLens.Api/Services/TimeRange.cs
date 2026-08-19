namespace GoogleHealthLens.Api.Services;

public record TimeRange(DateOnly From, DateOnly To)
{
    /// <summary>
    /// Shared timeframe convention for every read endpoint: preset=1d|7d|30d|1y|all, or an explicit from/to
    /// (ISO dates) for a custom range. "all" and custom-with-open-ends fall back to the caller-supplied
    /// data bounds so we never scan dates with no data.
    /// </summary>
    public static TimeRange Resolve(string? preset, DateOnly? from, DateOnly? to, DateOnly earliest, DateOnly latest)
    {
        if (from.HasValue || to.HasValue)
        {
            return new TimeRange(from ?? earliest, to ?? latest);
        }

        return preset switch
        {
            "1d" => new TimeRange(latest, latest),
            "7d" => new TimeRange(latest.AddDays(-6), latest),
            "30d" => new TimeRange(latest.AddDays(-29), latest),
            "1y" => new TimeRange(latest.AddYears(-1).AddDays(1), latest),
            _ => new TimeRange(earliest, latest),
        };
    }
}
