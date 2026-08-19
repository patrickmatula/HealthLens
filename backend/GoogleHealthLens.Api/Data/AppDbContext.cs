using GoogleHealthLens.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GoogleHealthLens.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<ImportRun> ImportRuns => Set<ImportRun>();
    public DbSet<DailyActivitySummary> DailyActivitySummaries => Set<DailyActivitySummary>();
    public DbSet<Workout> Workouts => Set<Workout>();
    public DbSet<WorkoutSplit> WorkoutSplits => Set<WorkoutSplit>();
    public DbSet<WorkoutSample> WorkoutSamples => Set<WorkoutSample>();
    public DbSet<PersonalRecord> PersonalRecords => Set<PersonalRecord>();
    public DbSet<SleepSession> SleepSessions => Set<SleepSession>();
    public DbSet<SleepStage> SleepStages => Set<SleepStage>();
    public DbSet<SleepScore> SleepScores => Set<SleepScore>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DailyActivitySummary>(e =>
        {
            e.HasKey(x => x.Date);
        });

        modelBuilder.Entity<Workout>(e =>
        {
            e.HasIndex(x => x.StartUtc);
            e.HasMany(x => x.Splits).WithOne().HasForeignKey(s => s.WorkoutId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<WorkoutSplit>(e =>
        {
            e.HasIndex(x => new { x.WorkoutId, x.SplitIndex }).IsUnique();
        });

        modelBuilder.Entity<WorkoutSample>(e =>
        {
            e.HasKey(x => new { x.WorkoutId, x.Timestamp });
        });

        modelBuilder.Entity<PersonalRecord>(e =>
        {
            e.HasIndex(x => new { x.NameLocalizationId, x.AchieveTimeUtc }).IsUnique();
        });

        modelBuilder.Entity<SleepSession>(e =>
        {
            e.HasIndex(x => x.StartUtc);
            e.HasMany(x => x.Stages).WithOne().HasForeignKey(s => s.SleepSessionId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Score).WithOne().HasForeignKey<SleepScore>(s => s.SleepSessionId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SleepStage>(e =>
        {
            e.HasIndex(x => x.SleepSessionId);
        });

        modelBuilder.Entity<SleepScore>(e =>
        {
            e.HasKey(x => x.SleepSessionId);
        });
    }
}
