using GoogleHealthLens.Api.Data;
using GoogleHealthLens.Api.Dtos;
using GoogleHealthLens.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GoogleHealthLens.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
public class DashboardController(DataSessionService session) : ControllerBase
{
    [HttpGet("overview")]
    public async Task<ActionResult<DashboardOverviewDto>> Overview(string? preset, DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        await using var db = session.CreateContext();

        if (!await db.DailyActivitySummaries.AnyAsync(ct))
        {
            var today = DateOnly.FromDateTime(DateTime.Now);
            return Ok(new DashboardOverviewDto(today, today, 0, 0, 0, 0, 0, 0, []));
        }

        var earliest = await db.DailyActivitySummaries.MinAsync(d => d.Date, ct);
        var latest = await db.DailyActivitySummaries.MaxAsync(d => d.Date, ct);
        var range = TimeRange.Resolve(preset, from, to, earliest, latest);

        var days = await db.DailyActivitySummaries
            .Where(d => d.Date >= range.From && d.Date <= range.To)
            .OrderBy(d => d.Date)
            .ToListAsync(ct);

        var withData = days.Count(d => d.Steps is > 0);

        return Ok(new DashboardOverviewDto(
            range.From,
            range.To,
            withData,
            days.Sum(d => (long)(d.Steps ?? 0)),
            days.Sum(d => d.DistanceMeters ?? 0),
            days.Sum(d => d.CaloriesTotal ?? 0),
            withData > 0 ? days.Sum(d => d.Steps ?? 0) / (double)withData : 0,
            withData > 0 ? days.Sum(d => d.ActiveMinutes ?? 0) / (double)withData : 0,
            days.Select(d => new DailyActivityPointDto(d.Date, d.Steps, d.DistanceMeters, d.CaloriesTotal, d.ActiveMinutes, d.SedentaryMinutes)).ToList()));
    }
}
