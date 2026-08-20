namespace HealthLens.Api.Models;

/// <summary>A pair of running/walking shoes the user tracks mileage for. Optional feature, off by default.</summary>
public class Shoe
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Brand { get; set; }
    public bool IsRetired { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
