using GoogleHealthLens.Api.Data;
using GoogleHealthLens.Api.Dtos;
using GoogleHealthLens.Api.Models;
using GoogleHealthLens.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GoogleHealthLens.Api.Controllers;

[ApiController]
[Route("api/import")]
public class ImportController(DataSessionService session, ImportJobRunner runner) : ControllerBase
{
    private const long MaxUploadBytes = 4L * 1024 * 1024 * 1024; // 4 GB — Takeout exports with years of intraday data can be large

    [HttpGet("current")]
    public async Task<ActionResult<ImportCurrentDto>> Current(CancellationToken ct)
    {
        if (!session.HasData)
        {
            return Ok(new ImportCurrentDto(false, session.Mode.ToString(), null, null, null));
        }

        await using var db = session.CreateContext();
        var last = await db.ImportRuns
            .Where(r => r.Status == ImportStatus.Completed)
            .OrderByDescending(r => r.Id)
            .FirstOrDefaultAsync(ct);

        return Ok(new ImportCurrentDto(true, session.Mode.ToString(), last?.CompletedAtUtc, last?.Scope, last?.RowsImported));
    }

    [HttpPost]
    [RequestSizeLimit(MaxUploadBytes)]
    public async Task<ActionResult<ImportStartedDto>> StartImport(
        IFormFile file,
        [FromForm] bool persistent,
        [FromForm] ImportScope scope,
        CancellationToken ct)
    {
        if (file.Length == 0)
        {
            return BadRequest("Keine Datei hochgeladen.");
        }

        if (!file.FileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("Bitte einen Google-Takeout-Export als .zip hochladen.");
        }

        var tempPath = Path.Combine(Path.GetTempPath(), "ghl-upload-" + Guid.NewGuid().ToString("N") + ".zip");
        await using (var stream = System.IO.File.Create(tempPath))
        {
            await file.CopyToAsync(stream, ct);
        }

        try
        {
            var jobId = runner.Start(tempPath, file.FileName, persistent, scope);
            return Ok(new ImportStartedDto(jobId));
        }
        catch (InvalidOperationException ex)
        {
            System.IO.File.Delete(tempPath);
            return Conflict(ex.Message);
        }
    }

    [HttpGet("{id:int}/status")]
    public ActionResult<ImportStatusDto> Status(int id)
    {
        var status = runner.GetStatus(id);
        if (status is null)
        {
            return NotFound();
        }

        return Ok(new ImportStatusDto(status.Id, status.Status.ToString(), status.CurrentStep, status.ProgressPercent, status.RowsImported, status.ErrorMessage));
    }
}
