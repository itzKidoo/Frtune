using System.Management;
using System.Runtime.InteropServices;
using Frtune.Core.Abstractions;
using Frtune.Core.Model;

namespace Frtune.Windows.Info;

public sealed record MemoryModule(string Slot, double SizeGb, int SpeedMhz, string Manufacturer, string PartNumber);
public sealed record DiskInfo(string Model, double SizeGb, string MediaType, string BusType);

/// <summary>Extra detail for the Hardware page (the "mini CPU-Z").</summary>
public sealed record HardwareDetails
{
    public string CpuName { get; init; } = "";
    public string CpuSocket { get; init; } = "";
    public int CpuMaxClockMhz { get; init; }
    public int CpuL2Kb { get; init; }
    public int CpuL3Kb { get; init; }
    public string Motherboard { get; init; } = "";
    public string Bios { get; init; } = "";
    public List<MemoryModule> Memory { get; init; } = new();
    public List<DiskInfo> Disks { get; init; } = new();
    public List<GpuInfo> Gpus { get; init; } = new();
    public string Os { get; init; } = "";
}

/// <summary>Detects the PC through WMI and the registry. Every query is wrapped so one failure can't break startup.</summary>
public sealed class SystemInfoProvider
{
    private readonly IRegistry _registry;
    public SystemInfoProvider(IRegistry registry) => _registry = registry;

    /// <summary>Never throws: if detection fails, a conservative default profile is used and the error is returned.</summary>
    public SystemProfile Detect(out string? error)
    {
        try
        {
            error = null;
            return DetectCore();
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return new SystemProfile { OsName = "Windows", Build = Environment.OSVersion.Version.Build, CpuThreads = Environment.ProcessorCount };
        }
    }

    private SystemProfile DetectCore()
    {
        const string cv = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";
        var build = int.TryParse(_registry.GetValue(RegHive.LocalMachine, cv, "CurrentBuildNumber")?.Data as string, out var b) ? b : Environment.OSVersion.Version.Build;
        var cpuName = Query("SELECT Name FROM Win32_Processor", o => ((string)o["Name"]).Trim()).FirstOrDefault() ?? "Unknown CPU";
        var cores = Query("SELECT NumberOfCores FROM Win32_Processor", o => Convert.ToInt32(o["NumberOfCores"])).Sum();
        var gpus = DetectGpus();
        var chassis = Query("SELECT ChassisTypes FROM Win32_SystemEnclosure", o => (ushort[]?)o["ChassisTypes"] ?? Array.Empty<ushort>()).SelectMany(x => x).ToList();
        var hasBattery = Query("SELECT Name FROM Win32_Battery", o => true).Any();
        // Portable chassis types: 8–10, 11, 14, 30–32 (SMBIOS spec).
        var laptopChassis = chassis.Any(c => c is 8 or 9 or 10 or 11 or 14 or 30 or 31 or 32);
        var osName = build >= 22000 ? "Windows 11" : "Windows 10";

        return new SystemProfile
        {
            OsName = osName,
            Build = build,
            Ubr = (int)(_registry.GetValue(RegHive.LocalMachine, cv, "UBR")?.AsDWord ?? 0),
            DisplayVersion = _registry.GetValue(RegHive.LocalMachine, cv, "DisplayVersion")?.Data as string ?? "",
            Edition = _registry.GetValue(RegHive.LocalMachine, cv, "EditionID")?.Data as string ?? "Unknown",
            IsLaptop = laptopChassis || hasBattery,
            HasBattery = hasBattery,
            CpuName = cpuName,
            CpuCores = cores > 0 ? cores : Environment.ProcessorCount,
            CpuThreads = Environment.ProcessorCount,
            CpuHasIntegratedGpu = gpus.Any(g => g.Vendor == GpuVendor.Intel) || !IsIntelFSku(cpuName) && cpuName.Contains("Intel", StringComparison.OrdinalIgnoreCase),
            Gpus = gpus,
            TotalRamGb = Math.Round(TotalPhysicalMemory() / 1073741824.0, 1),
            SystemDrive = Environment.GetEnvironmentVariable("SystemDrive") ?? "C:",
            SystemDriveIsSsd = SystemDriveIsSsd(),
            HasWifi = Query("SELECT Name FROM Win32_NetworkAdapter WHERE PhysicalAdapter = TRUE", o => (string)o["Name"])
                .Any(n => n.Contains("Wi-Fi", StringComparison.OrdinalIgnoreCase) || n.Contains("Wireless", StringComparison.OrdinalIgnoreCase) || n.Contains("802.11")),
            HasBluetooth = Query("SELECT Name FROM Win32_PnPEntity WHERE PNPClass = 'Bluetooth'", o => true).Any(),
            HasPhysicalPrinter = Query("SELECT Name, PortName FROM Win32_Printer", o => ((string)o["Name"], o["PortName"] as string ?? ""))
                .Any(p => !IsVirtualPrinter(p.Item1, p.Item2)),
        };
    }

    private static bool IsIntelFSku(string cpu) => System.Text.RegularExpressions.Regex.IsMatch(cpu, @"i\d-\d{4,5}K?F\b");

    private static bool IsVirtualPrinter(string name, string port) =>
        name.Contains("PDF", StringComparison.OrdinalIgnoreCase) || name.Contains("XPS", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("OneNote", StringComparison.OrdinalIgnoreCase) || name.Equals("Fax", StringComparison.OrdinalIgnoreCase) ||
        port.StartsWith("PORTPROMPT", StringComparison.OrdinalIgnoreCase) || port.Equals("SHRFAX:", StringComparison.OrdinalIgnoreCase) ||
        port.Equals("nul:", StringComparison.OrdinalIgnoreCase);

    private List<GpuInfo> DetectGpus()
    {
        var gpus = Query("SELECT Name, AdapterCompatibility, DriverVersion, PNPDeviceID FROM Win32_VideoController", o =>
        {
            var name = o["Name"] as string ?? "GPU";
            var vendor = GpuInfo.DetectVendor(name + " " + (o["AdapterCompatibility"] as string ?? ""));
            return new GpuInfo(name, vendor, 0, o["DriverVersion"] as string ?? "");
        }).Where(g => !g.Name.Contains("Basic Display", StringComparison.OrdinalIgnoreCase) && !g.Name.Contains("Remote", StringComparison.OrdinalIgnoreCase)).ToList();

        // Win32_VideoController.AdapterRAM is 32-bit and caps at 4 GB, so read the 64-bit value from the driver key.
        const string cls = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}";
        // Adapter instances are the 4-digit subkeys; skip "Properties" and "Configuration" (SYSTEM-only).
        foreach (var sub in _registry.GetSubKeyNames(RegHive.LocalMachine, cls).Where(n => n.Length == 4 && n.All(char.IsDigit)))
        {
            var desc = _registry.GetValue(RegHive.LocalMachine, $@"{cls}\{sub}", "DriverDesc")?.Data as string;
            var mem = _registry.GetValue(RegHive.LocalMachine, $@"{cls}\{sub}", "HardwareInformation.qwMemorySize");
            if (desc is null || mem is null) continue;
            var bytes = mem.Kind == RegKind.QWord ? (double)(ulong)mem.Data : mem.Kind == RegKind.Binary ? BitConverter.ToUInt64(((byte[])mem.Data).Concat(new byte[8]).Take(8).ToArray()) : 0;
            var i = gpus.FindIndex(g => g.Name == desc);
            if (i >= 0) gpus[i] = gpus[i] with { VramGb = Math.Round(bytes / 1073741824.0, 1) };
        }
        return gpus;
    }

    private static bool SystemDriveIsSsd()
    {
        try
        {
            var letter = (Environment.GetEnvironmentVariable("SystemDrive") ?? "C:").TrimEnd(':');
            using var part = new ManagementObjectSearcher(@"root\Microsoft\Windows\Storage", $"SELECT DiskNumber FROM MSFT_Partition WHERE DriveLetter = '{letter}'");
            var disk = part.Get().Cast<ManagementObject>().Select(o => Convert.ToUInt32(o["DiskNumber"])).FirstOrDefault();
            using var pd = new ManagementObjectSearcher(@"root\Microsoft\Windows\Storage", $"SELECT MediaType FROM MSFT_PhysicalDisk WHERE DeviceId = '{disk}'");
            var media = pd.Get().Cast<ManagementObject>().Select(o => Convert.ToUInt16(o["MediaType"])).FirstOrDefault();
            return media != 3; // 3 = HDD, 4 = SSD, 0 = unspecified (usually NVMe/virtual: treat as SSD)
        }
        catch { return true; }
    }

    public HardwareDetails Details()
    {
        try { return DetailsCore(); }
        catch { return new HardwareDetails(); }
    }

    private HardwareDetails DetailsCore()
    {
        var cpu = Query("SELECT Name, SocketDesignation, MaxClockSpeed, L2CacheSize, L3CacheSize FROM Win32_Processor", o => o).FirstOrDefault();
        return new HardwareDetails
        {
            CpuName = cpu?["Name"] as string ?? "",
            CpuSocket = cpu?["SocketDesignation"] as string ?? "",
            CpuMaxClockMhz = Convert.ToInt32(cpu?["MaxClockSpeed"] ?? 0),
            CpuL2Kb = Convert.ToInt32(cpu?["L2CacheSize"] ?? 0),
            CpuL3Kb = Convert.ToInt32(cpu?["L3CacheSize"] ?? 0),
            Motherboard = Query("SELECT Manufacturer, Product FROM Win32_BaseBoard", o => $"{o["Manufacturer"]} {o["Product"]}").FirstOrDefault() ?? "",
            Bios = Query("SELECT SMBIOSBIOSVersion, ReleaseDate FROM Win32_BIOS", o =>
                $"{o["SMBIOSBIOSVersion"]} ({(o["ReleaseDate"] is string d && d.Length >= 8 ? $"{d[..4]}-{d[4..6]}-{d[6..8]}" : "?")})").FirstOrDefault() ?? "",
            Memory = Query("SELECT DeviceLocator, Capacity, ConfiguredClockSpeed, Speed, Manufacturer, PartNumber FROM Win32_PhysicalMemory", o =>
                new MemoryModule(o["DeviceLocator"] as string ?? "", Convert.ToUInt64(o["Capacity"] ?? 0UL) / 1073741824.0,
                    Convert.ToInt32(o["ConfiguredClockSpeed"] ?? o["Speed"] ?? 0), (o["Manufacturer"] as string ?? "").Trim(), (o["PartNumber"] as string ?? "").Trim())).ToList(),
            Disks = Query("SELECT FriendlyName, Size, MediaType, BusType FROM MSFT_PhysicalDisk", o =>
                new DiskInfo(o["FriendlyName"] as string ?? "", Convert.ToUInt64(o["Size"] ?? 0UL) / 1e9,
                    Convert.ToUInt16(o["MediaType"] ?? 0) switch { 3 => "HDD", 4 => "SSD", _ => "Unknown" },
                    Convert.ToUInt16(o["BusType"] ?? 0) switch { 17 => "NVMe", 11 => "SATA", 7 => "USB", 8 => "RAID", _ => "Other" }),
                @"root\Microsoft\Windows\Storage").ToList(),
            Gpus = DetectGpus(),
            Os = Query("SELECT Caption, Version FROM Win32_OperatingSystem", o => $"{o["Caption"]} {o["Version"]}").FirstOrDefault() ?? "",
        };
    }

    private static List<T> Query<T>(string wql, Func<ManagementBaseObject, T> map, string scope = @"root\cimv2")
    {
        var list = new List<T>();
        try
        {
            using var s = new ManagementObjectSearcher(scope, wql);
            foreach (var o in s.Get())
                using (o)
                {
                    try { list.Add(map(o)); } catch { }
                }
        }
        catch { }
        return list;
    }

    public static ulong TotalPhysicalMemory()
    {
        var m = new MemoryStatusEx();
        return GlobalMemoryStatusEx(m) ? m.ullTotalPhys : 0;
    }

    public static (ulong Total, ulong Available, uint LoadPercent) MemoryStatus()
    {
        var m = new MemoryStatusEx();
        return GlobalMemoryStatusEx(m) ? (m.ullTotalPhys, m.ullAvailPhys, m.dwMemoryLoad) : (0, 0, 0);
    }

    [StructLayout(LayoutKind.Sequential)]
    private sealed class MemoryStatusEx
    {
        public uint dwLength = (uint)Marshal.SizeOf<MemoryStatusEx>();
        public uint dwMemoryLoad;
        public ulong ullTotalPhys, ullAvailPhys, ullTotalPageFile, ullAvailPageFile, ullTotalVirtual, ullAvailVirtual, ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx([In, Out] MemoryStatusEx buffer);
}
