namespace HealthLens.Api.Services.Import;

/// <summary>
/// "Does this window overlap anything I already know about?", bucketed by calendar day. The legacy
/// gap-fill importers ask that once per legacy entry against every modern entry, which is quadratic
/// on a multi-year export; every interval here spans a day or two, so the day buckets turn it into a
/// couple of comparisons.
/// </summary>
public sealed class IntervalIndex
{
    private readonly Dictionary<DateOnly, List<(DateTime Start, DateTime End)>> _byDay = [];

    public void Add(DateTime start, DateTime end)
    {
        foreach (var day in DaysTouched(start, end))
        {
            if (!_byDay.TryGetValue(day, out var bucket))
            {
                bucket = [];
                _byDay[day] = bucket;
            }

            bucket.Add((start, end));
        }
    }

    public bool Overlaps(DateTime start, DateTime end)
    {
        foreach (var day in DaysTouched(start, end))
        {
            if (!_byDay.TryGetValue(day, out var bucket))
            {
                continue;
            }

            foreach (var (otherStart, otherEnd) in bucket)
            {
                if (otherStart < end && start < otherEnd)
                {
                    return true;
                }
            }
        }

        return false;
    }

    public static IEnumerable<DateOnly> DaysTouched(DateTime start, DateTime end)
    {
        var day = DateOnly.FromDateTime(start);
        var last = DateOnly.FromDateTime(end);
        while (day <= last)
        {
            yield return day;
            day = day.AddDays(1);
        }
    }
}
