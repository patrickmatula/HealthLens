namespace HealthLens.Api.Dtos;

public record RestingHeartRatePointDto(DateOnly Date, double Bpm);

public record AzmDayDto(DateOnly Date, int FatBurnMinutes, int CardioMinutes, int PeakMinutes);

public record HrvDayDto(DateOnly Date, double RmssdMs, double NonRemHrBpm, double Entropy);

public record RespiratoryRatePointDto(DateOnly Date, double BreathsPerMinute);

public record HeartOverviewDto(
    DateOnly From,
    DateOnly To,
    double? AvgRestingHeartRate,
    double? AvgHrv,
    IReadOnlyList<RestingHeartRatePointDto> RestingHeartRate,
    IReadOnlyList<AzmDayDto> ActiveZoneMinutes,
    IReadOnlyList<HrvDayDto> Hrv,
    IReadOnlyList<RespiratoryRatePointDto> RespiratoryRate);

public record IntradayHeartRatePointDto(DateTime MinuteUtc, double MinBpm, double AvgBpm, double MaxBpm);
