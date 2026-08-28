using System.Text.Json;

namespace HealthLens.Api.Services;

/// <summary>
/// Per-category default shoe ("every new 'Lauf' gets Shoe X automatically"), applied when a genuinely
/// new workout is created by the Takeout importer or the Google Health sync — never overwrites a shoe
/// the user already assigned by hand. A flat JSON file rather than an EF entity/table, same reasoning
/// as GoogleHealthCredentialStore: changing a default must never trip the schema-drift
/// archive-and-reimport cycle in DataSessionService.
/// </summary>
public sealed class ShoeDefaultsStore(IWebHostEnvironment env)
{
    private string FilePath => Path.Combine(env.ContentRootPath, "App_Data", "shoe-defaults.json");

    public Dictionary<string, int> Load()
    {
        if (!File.Exists(FilePath))
        {
            return [];
        }

        var json = File.ReadAllText(FilePath);
        return JsonSerializer.Deserialize<Dictionary<string, int>>(json) ?? [];
    }

    public void Save(Dictionary<string, int> defaults)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(defaults));
    }

    public int? GetDefaultShoeId(string category) => Load().TryGetValue(category, out var id) ? id : null;
}
