using System.IO;
using System.Text.Json;

namespace Frtune.App.Services;

/// <summary>User preferences, stored in %LocalAppData%\frtune\settings.json.</summary>
public sealed class AppSettings
{
    public string AccentHex { get; set; } = "#8B5CF6";
    public int UpdatePauseDays { get; set; } = 7;
    public bool CreateRestorePoint { get; set; } = true;
    public bool ExportRawBackup { get; set; } = true;
    public bool ShowNotApplicable { get; set; }

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static AppSettings Load(string path)
    {
        try
        {
            if (File.Exists(path)) return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path)) ?? new();
        }
        catch { /* corrupt settings: fall back to defaults */ }
        return new();
    }

    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(this, Options));
    }
}
