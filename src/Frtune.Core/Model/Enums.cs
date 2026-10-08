namespace Frtune.Core.Model;

public enum TweakCategory
{
    Gaming,
    Performance,
    Network,
    Privacy,
    Debloat,
    Services,
    Input,
    Power,
    Visuals,
    Advanced,
}

public enum TweakRisk
{
    Safe,
    Moderate,
    Advanced,
}

/// <summary>Preset profiles. A tweak lists every preset it belongs to.</summary>
[Flags]
public enum Preset
{
    None = 0,
    Gaming = 1,
    Balanced = 2,
    LowEnd = 4,
    Privacy = 8,
    MaxPerformance = 16,
}

public enum RestartKind
{
    None = 0,
    ExplorerRestart = 1,
    SignOut = 2,
    Reboot = 3,
}

public enum GpuVendor
{
    Unknown,
    Nvidia,
    Amd,
    Intel,
}
