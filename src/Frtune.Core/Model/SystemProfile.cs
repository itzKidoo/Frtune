namespace Frtune.Core.Model;

/// <summary>
/// What the app detected about this PC. Tweaks use it to decide whether they apply
/// (<see cref="Tweaks.ITweak.IsApplicable"/>) and whether to recommend themselves.
/// </summary>
public sealed record SystemProfile
{
    public string OsName { get; init; } = "Windows";
    public int Build { get; init; } = 19045;
    public int Ubr { get; init; }
    public string DisplayVersion { get; init; } = "";
    /// <summary>EditionID from the registry, e.g. Core, Professional, Enterprise, Education.</summary>
    public string Edition { get; init; } = "Professional";

    public bool IsLaptop { get; init; }
    public bool HasBattery { get; init; }

    public string CpuName { get; init; } = "";
    public int CpuCores { get; init; } = 4;
    public int CpuThreads { get; init; } = 4;
    /// <summary>True for CPUs with an integrated GPU. "F" Intel SKUs (e.g. i3-9100F) have none.</summary>
    public bool CpuHasIntegratedGpu { get; init; }

    public IReadOnlyList<GpuInfo> Gpus { get; init; } = Array.Empty<GpuInfo>();

    public double TotalRamGb { get; init; } = 16;
    public bool SystemDriveIsSsd { get; init; } = true;
    public string SystemDrive { get; init; } = "C:";

    public bool HasWifi { get; init; }
    public bool HasBluetooth { get; init; }
    public bool HasPhysicalPrinter { get; init; }

    public bool IsWindows11 => Build >= 22000;
    public bool IsEnterpriseOrEducation =>
        Edition.Contains("Enterprise", StringComparison.OrdinalIgnoreCase) ||
        Edition.Contains("Education", StringComparison.OrdinalIgnoreCase) ||
        Edition.Contains("Server", StringComparison.OrdinalIgnoreCase);

    public bool HasGpu(GpuVendor vendor) => Gpus.Any(g => g.Vendor == vendor);
    public bool HasDiscreteGpu => Gpus.Any(g => g.Vendor is GpuVendor.Nvidia or GpuVendor.Amd);
    public bool IsMultiGpu => Gpus.Count(g => g.Vendor != GpuVendor.Unknown) > 1;
    public GpuInfo? PrimaryGpu => Gpus.FirstOrDefault(g => g.Vendor is GpuVendor.Nvidia or GpuVendor.Amd) ?? Gpus.FirstOrDefault();
    public long TotalRamKb => (long)(TotalRamGb * 1024 * 1024);

    public string Summary =>
        $"{OsName} build {Build}.{Ubr} ({Edition}) · {(IsLaptop ? "Laptop" : "Desktop")} · {CpuName} ({CpuCores}C/{CpuThreads}T) · " +
        $"{PrimaryGpu?.Name ?? "Unknown GPU"} · {TotalRamGb:0.#} GB RAM · {(SystemDriveIsSsd ? "SSD" : "HDD")} system drive";
}

public sealed record GpuInfo(string Name, GpuVendor Vendor, double VramGb, string DriverVersion)
{
    public static GpuVendor DetectVendor(string nameOrCompany)
    {
        var s = nameOrCompany.ToUpperInvariant();
        if (s.Contains("NVIDIA") || s.Contains("GEFORCE") || s.Contains("QUADRO") || s.Contains("RTX")) return GpuVendor.Nvidia;
        if (s.Contains("AMD") || s.Contains("RADEON") || s.Contains("ATI ")) return GpuVendor.Amd;
        if (s.Contains("INTEL")) return GpuVendor.Intel;
        return GpuVendor.Unknown;
    }
}
