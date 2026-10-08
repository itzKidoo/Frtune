using System.Text.Json;

namespace Frtune.Core.Engine;

/// <summary>An exported tweak configuration (Settings → Export profile). Only ids, never values.</summary>
public sealed record ProfileFile
{
    public string App { get; init; } = "frtune";
    public int Version { get; init; } = 1;
    public string Name { get; init; } = "My profile";
    public DateTimeOffset Created { get; init; } = DateTimeOffset.Now;
    public List<string> Tweaks { get; init; } = new();

    public string ToJson() => JsonSerializer.Serialize(this, Json.Options);

    public static ProfileFile FromJson(string json)
    {
        var p = JsonSerializer.Deserialize<ProfileFile>(json, Json.Options)
            ?? throw new FormatException("Empty profile file.");
        if (p.App != "frtune") throw new FormatException("This is not a frtune profile.");
        if (p.Version > 1) throw new FormatException($"Profile version {p.Version} is newer than this app supports.");
        return p;
    }
}
