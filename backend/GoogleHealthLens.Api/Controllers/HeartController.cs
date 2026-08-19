using GoogleHealthLens.Api.Data;
using GoogleHealthLens.Api.Dtos;
using GoogleHealthLens.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GoogleHealthLens.Api.Controllers;

[ApiController]
[Route("api/heart")]
public class HeartController(DataSessionService session) : ControllerBase
{
    [HttpGet("overview")]
    public async Task<ActionResult<HeartOverviewDto>> Overview(string? preset, DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        await using var db = session.CreateContext();

        var hasAny = await db.RestingHeartRateDailies.AnyAsync(ct) || await db.HrvDailySummaries.AnyAsync(ct);
        if (!hasAny)
        {
            var today = DateOnly.FromDateTime(DateTime.Now);
            return Ok(new HeartOverviewDto(today, today, null, null, [], [], [], []));
        }

        var dates = new List<DateOnly>();
        if (await db.RestingHeartRateDailies.AnyAsync(ct))
        {
            dates.Add(await db.RestingHeartRateDailies.MinAsync(x => x.Date, ct));
            dates.Add(await db.RestingHeartRateDailies.MaxAsync(x => x.Date, ct));
        }

        if (await db.HrvDailySummaries.AnyAsync(ct))
        {
            dates.Add(await db.HrvDailySummaries.MinAsync(x => x.Date, ct));
            dates.Add(await db.HrvDailySummaries.MaxAsync(x => x.Date, ct));
        }

        var earliest = dates.Min();
        var latest = dates.Max();
        var range = TimeRange.Resolve(preset, from, to, earliest, latest);

        var rhr = await db.RestingHeartRateDailies.Where(x => x.Date >= range.From && x.Date <= range.To).OrderBy(x => x.Date).ToListAsync(ct);
        var azm = await db.HeartRateZoneMinutesDailies.Where(x => x.Date >= range.From && x.Date <= range.To).ToListAsync(ct);
        var hrv = await db.HrvDailySummaries.Where(x => x.Date >= range.From && x.Date <= range.To).OrderBy(x => x.Date).ToListAsync(ct);
        var resp = await db.RespiratoryRateDailies.Where(x => x.Date >= range.From && x.Date <= range.To).OrderBy(x => x.Date).ToListAsync(ct);

        var azmByDay = azm.GroupBy(x => x.Date).Select(g => new AzmDayDto(
            g.Key,
            g.Where(z => z.Zone == "FAT_BURN").Sum(z => z.Minutes),
            g.Where(z => z.Zone == "CARDIO").Sum(z => z.Minutes),
            g.Where(z => z.Zone == "PEAK").Sum(z => z.Minutes))).OrderBy(x => x.Date).ToList();

        return Ok(new HeartOverviewDto(
            range.From,
            range.To,
            rhr.Count > 0 ? rhr.Average(x => x.Bpm) : null,
            hrv.Count > 0 ? hrv.Average(x => x.RmssdMs) : null,
            rhr.Select(x => new RestingHeartRatePointDto(x.Date, x.Bpm)).ToList(),
            azmByDay,
            hrv.Select(x => new HrvDayDto(x.Date, x.RmssdMs, x.NonRemHrBpm, x.Entropy)).ToList(),
            resp.Select(x => new RespiratoryRatePointDto(x.Date, x.BreathsPerMinute)).ToList()));
    }

    [HttpGet("intraday")]
    public async Task<ActionResult<IReadOnlyList<IntradayHeartRatePointDto>>> Intraday(DateOnly date, CancellationToken ct)
    {
        await using var db = session.CreateContext();
        var start = DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
        var end = DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MaxValue), DateTimeKind.Utc);

        var points = await db.HeartRateMinutelies
            .Where(x => x.MinuteUtc >= start && x.MinuteUtc <= end)
            .OrderBy(x => x.MinuteUtc)
            .ToListAsync(ct);

        return Ok(points.Select(x => new IntradayHeartRatePointDto(x.MinuteUtc, x.MinBpm, x.AvgBpm, x.MaxBpm)).ToList());
    }
}
