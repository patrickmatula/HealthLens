using System.Globalization;
using HealthLens.Api.Models;
using HealthLens.Api.Services.Csv;

namespace HealthLens.Api.Services.Importers;

public partial class WorkoutsImporter
{
    private static void ImportPerSecondStreams(string folder, Dictionary<long, Workout> workouts, Dictionary<long, Dictionary<DateTime, WorkoutSample>> samples)
    {
        var workoutsByDate = new Dictionary<DateOnly, List<Workout>>();
        foreach (var w in workouts.Values)
        {
            foreach (var date in DatesTouched(w.StartUtc - MatchBuffer, w.EndUtc + MatchBuffer))
            {
                if (!workoutsByDate.TryGetValue(date, out var list))
                {
                    list = [];
                    workoutsByDate[date] = list;
                }

                list.Add(w);
            }
        }

        foreach (var (date, dateWorkouts) in workoutsByDate)
        {
            var ds = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

            MergeFile(folder, $"gps_location_{ds}.csv", dateWorkouts, samples, (csv, latCol, lonCol, altCol, s) =>
            {
                s.Latitude = csv.GetDouble(latCol);
                s.Longitude = csv.GetDouble(lonCol);
                s.AltitudeMeters = csv.GetDouble(altCol);
                workouts[s.WorkoutId].HasGps = true;
            }, "latitude", "longitude", "altitude");

            MergeFile(folder, $"cadence_{ds}.csv", dateWorkouts, samples, (csv, col, _, _, s) =>
            {
                s.CadenceSpm = csv.GetDouble(col);
            }, "steps per minute");

            MergeFile(folder, $"speed_{ds}.csv", dateWorkouts, samples, (csv, col, _, _, s) =>
            {
                var speed = csv.GetDouble(col);
                s.SpeedKmh = speed;
                s.PaceSecPerKm = speed is > 0 ? 3600.0 / speed : null;
            }, "speed");

            MergeFile(folder, $"stride_length_{ds}.csv", dateWorkouts, samples, (csv, col, _, _, s) =>
            {
                s.StrideLengthCm = csv.GetDouble(col);
            }, "stride length");

            MergeFile(folder, $"vertical_oscillation_{ds}.csv", dateWorkouts, samples, (csv, col, _, _, s) =>
            {
                s.VerticalOscillationCm = csv.GetDouble(col);
            }, "vertical oscillation");

            MergeFile(folder, $"vertical_ratio_{ds}.csv", dateWorkouts, samples, (csv, col, _, _, s) =>
            {
                s.VerticalRatioPercent = csv.GetDouble(col);
            }, "vertical ratio");

            MergeFile(folder, $"ground_contact_time_{ds}.csv", dateWorkouts, samples, (csv, col, _, _, s) =>
            {
                s.GroundContactTimeMs = csv.GetDouble(col);
            }, "ground contact time");

            MergeFile(folder, $"heart_rate_{ds}.csv", dateWorkouts, samples, (csv, col, _, _, s) =>
            {
                s.HeartRateBpm = csv.GetDouble(col);
            }, "beats per minute");
        }
    }

    private static void MergeFile(
        string folder,
        string fileName,
        List<Workout> dateWorkouts,
        Dictionary<long, Dictionary<DateTime, WorkoutSample>> samples,
        Action<CsvCursor, int, int, int, WorkoutSample> apply,
        string column1,
        string column2 = "",
        string column3 = "")
    {
        using var csv = CsvCursor.Open(Path.Combine(folder, fileName));
        if (csv is null)
        {
            return;
        }

        var timestampCol = csv.Column("timestamp");
        if (timestampCol < 0)
        {
            return;
        }

        var col1 = csv.Column(column1);
        var col2 = column2.Length > 0 ? csv.Column(column2) : -1;
        var col3 = column3.Length > 0 ? csv.Column(column3) : -1;

        while (csv.NextRow())
        {
            if (csv.GetUtc(timestampCol) is not { } ts)
            {
                continue;
            }

            var rounded = new DateTime(ts.Ticks - (ts.Ticks % TimeSpan.TicksPerSecond), DateTimeKind.Utc);

            foreach (var w in dateWorkouts)
            {
                if (rounded < w.StartUtc - MatchBuffer || rounded > w.EndUtc + MatchBuffer)
                {
                    continue;
                }

                if (!samples.TryGetValue(w.Id, out var byTime))
                {
                    byTime = [];
                    samples[w.Id] = byTime;
                }

                if (!byTime.TryGetValue(rounded, out var sample))
                {
                    sample = new WorkoutSample { WorkoutId = w.Id, Timestamp = rounded };
                    byTime[rounded] = sample;
                }

                apply(csv, col1, col2, col3, sample);
            }
        }
    }

    private static IEnumerable<DateOnly> DatesTouched(DateTime start, DateTime end)
    {
        var d = DateOnly.FromDateTime(start);
        var last = DateOnly.FromDateTime(end);
        while (d <= last)
        {
            yield return d;
            d = d.AddDays(1);
        }
    }
}
