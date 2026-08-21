namespace HealthLens.Api.Models;

/// <summary>
/// The fixed set of body measurements this app knows how to chart and (where a medical reference
/// range exists) assess. Deliberately a closed set rather than free-form named measurements — it's
/// what lets BodyReferences compute BMI/waist-to-hip-ratio/body-fat categories per entry.
/// </summary>
public enum BodyMeasurementType
{
    WeightKg,
    BodyFatPercent,
    WaistCm,
    HipCm,
    ChestCm,
    NeckCm,
    BicepCm,
    ThighCm,
    CalfCm,
}

/// <summary>
/// Bicep/thigh/calf come in pairs, so those three types carry a side; everything else is always
/// <see cref="None"/>. Kept as a real enum value rather than a nullable column — SQLite's PRIMARY KEY
/// handling of NULL columns in a composite key is inconsistent, and "None" is exactly the right default
/// for the six unpaired types anyway.
/// </summary>
public enum BodySide
{
    None,
    Left,
    Right,
}

/// <summary>One measurement, one day (one side, for the paired types). Optional feature — the table exists whether or not it's ever used.</summary>
public class BodyMeasurement
{
    public DateOnly Date { get; set; }
    public BodyMeasurementType Type { get; set; }
    public BodySide Side { get; set; }
    public double Value { get; set; }
}

/// <summary>
/// Single-row profile (Id is always 1) holding what the BMI/waist-to-hip-ratio/body-fat reference
/// ranges need and can't derive from a measurement alone. Both optional: without a height, BMI is
/// skipped; without a sex, the sex-specific ranges are skipped — the trend charts still work regardless.
/// </summary>
public class BodyProfile
{
    public int Id { get; set; }
    public double? HeightCm { get; set; }
    public string? Sex { get; set; }
}
