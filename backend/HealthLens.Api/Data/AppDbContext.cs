using HealthLens.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace HealthLens.Api.Data;

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
    public DbSet<RestingHeartRateDaily> RestingHeartRateDailies => Set<RestingHeartRateDaily>();
    public DbSet<HeartRateZoneMinutesDaily> HeartRateZoneMinutesDailies => Set<HeartRateZoneMinutesDaily>();
    public DbSet<HrvDailySummary> HrvDailySummaries => Set<HrvDailySummary>();
    public DbSet<HrvDetail> HrvDetails => Set<HrvDetail>();
    public DbSet<RespiratoryRateDaily> RespiratoryRateDailies => Set<RespiratoryRateDaily>();
    public DbSet<HeartRateMinutely> HeartRateMinutelies => Set<HeartRateMinutely>();
    public DbSet<StressScoreDaily> StressScoreDailies => Set<StressScoreDaily>();
    public DbSet<DailyReadiness> DailyReadinesses => Set<DailyReadiness>();
    public DbSet<SpO2Daily> SpO2Dailies => Set<SpO2Daily>();
    public DbSet<TemperatureNightly> TemperatureNightlies => Set<TemperatureNightly>();
    public DbSet<Shoe> Shoes => Set<Shoe>();

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
            e.HasOne<Shoe>().WithMany().HasForeignKey(x => x.ShoeId).OnDelete(DeleteBehavior.SetNull);
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

        modelBuilder.Entity<RestingHeartRateDaily>(e => e.HasKey(x => x.Date));
        modelBuilder.Entity<HeartRateZoneMinutesDaily>(e => e.HasKey(x => new { x.Date, x.Zone }));
        modelBuilder.Entity<HrvDailySummary>(e => e.HasKey(x => x.Date));
        modelBuilder.Entity<HrvDetail>(e => e.HasKey(x => x.TimestampUtc));
        modelBuilder.Entity<RespiratoryRateDaily>(e => e.HasKey(x => x.Date));
        modelBuilder.Entity<HeartRateMinutely>(e => e.HasKey(x => x.MinuteUtc));

        modelBuilder.Entity<StressScoreDaily>(e => e.HasKey(x => x.Date));
        modelBuilder.Entity<DailyReadiness>(e => e.HasKey(x => x.Date));
        modelBuilder.Entity<SpO2Daily>(e => e.HasKey(x => x.Date));
        modelBuilder.Entity<TemperatureNightly>(e => e.HasKey(x => x.Date));
    }
}
