using System.Text.Json;

namespace SymatoIME;

/// <summary>
/// Application settings with JSON persistence
/// </summary>
public class Settings
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SymatoIME",
        "settings.json");

    public bool ImeEnabled { get; set; } = true;
    public bool KeyRemapEnabled { get; set; } = true;
    public bool VolumeControlEnabled { get; set; } = true;
    public bool StartWithWindows { get; set; } = false;

    public static Settings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                string json = File.ReadAllText(SettingsPath);
                return JsonSerializer.Deserialize<Settings>(json) ?? new Settings();
            }
        }
        catch
        {
            // Return default settings on error
        }
        return new Settings();
    }

    public void Save()
    {
        try
        {
            string? directory = Path.GetDirectoryName(SettingsPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(this, options);
            File.WriteAllText(SettingsPath, json);
        }
        catch
        {
            // Silently fail if settings cannot be saved
        }
    }
}
