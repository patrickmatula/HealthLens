using GoogleHealthLens.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GoogleHealthLens.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<ImportRun> ImportRuns => Set<ImportRun>();
    public DbSet<DailyActivitySummary> DailyActivitySummaries => Set<DailyActivitySummary>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DailyActivitySummary>(e =>
        {
            e.HasKey(x => x.Date);
        });
    }
}
