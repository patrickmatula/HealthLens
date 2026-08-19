using System.Globalization;
using CsvHelper;
using HealthLens.Api.Models;

namespace HealthLens.Api.Services.Importers;

public partial class WorkoutsImporter
{
    private static readonly TimeSpan MatchBuffer = TimeSpan.FromMinutes(5);

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

            MergeFile(folder, $"gps_location_{ds}.csv", dateWorkouts, samples, (csv, s) =>
            {
                s.Latitude = ParseNullableDouble(csv.GetField("latitude"));
                s.Longitude = ParseNullableDouble(csv.GetField("longitude"));
                s.AltitudeMeters = ParseNullableDouble(csv.GetField("altitude"));
                workouts[s.WorkoutId].HasGps = true;
            });

            MergeFile(folder, $"cadence_{ds}.csv", dateWorkouts, samples, (csv, s) =>
            {
                s.CadenceSpm = ParseNullableDouble(csv.GetField("steps per minute"));
            });

            MergeFile(folder, $"speed_{ds}.csv", dateWorkouts, samples, (csv, s) =>
            {
                var speed = ParseNullableDouble(csv.GetField("speed"));
                s.SpeedKmh = speed;
                s.PaceSecPerKm = speed is > 0 ? 3600.0 / speed : null;
            });

            MergeFile(folder, $"stride_length_{ds}.csv", dateWorkouts, samples, (csv, s) =>
            {
                s.StrideLengthCm = ParseNullableDouble(csv.GetField("stride length"));
            });

            MergeFile(folder, $"vertical_oscillation_{ds}.csv", dateWorkouts, samples, (csv, s) =>
            {
                s.VerticalOscillationCm = ParseNullableDouble(csv.GetField("vertical oscillation"));
            });

            MergeFile(folder, $"vertical_ratio_{ds}.csv", dateWorkouts, samples, (csv, s) =>
            {
                s.VerticalRatioPercent = ParseNullableDouble(csv.GetField("vertical ratio"));
            });

            MergeFile(folder, $"ground_contact_time_{ds}.csv", dateWorkouts, samples, (csv, s) =>
            {
                s.GroundContactTimeMs = ParseNullableDouble(csv.GetField("ground contact time"));
            });

            MergeFile(folder, $"heart_rate_{ds}.csv", dateWorkouts, samples, (csv, s) =>
            {
                s.HeartRateBpm = ParseNullableDouble(csv.GetField("beats per minute"));
            });
        }
    }

    private static void MergeFile(
        string folder,
        string fileName,
        List<Workout> dateWorkouts,
        Dictionary<long, Dictionary<DateTime, WorkoutSample>> samples,
        Action<CsvReader, WorkoutSample> apply)
    {
        var path = Path.Combine(folder, fileName);
        if (!File.Exists(path))
        {
            return;
        }

        using var reader = new StreamReader(path);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
        csv.Read();
        csv.ReadHeader();

        while (csv.Read())
        {
            var ts = ParseUtc(csv.GetField("timestamp"));
            if (ts is null)
            {
                continue;
            }

            var rounded = new DateTime(ts.Value.Ticks - (ts.Value.Ticks % TimeSpan.TicksPerSecond), DateTimeKind.Utc);

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

                apply(csv, sample);
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
