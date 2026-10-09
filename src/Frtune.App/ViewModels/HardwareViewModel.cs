using CommunityToolkit.Mvvm.ComponentModel;
using Frtune.Windows.Info;

namespace Frtune.App.ViewModels;

public sealed record SpecRow(string Label, string Value);
public sealed record SpecGroup(string Title, string Glyph, IReadOnlyList<SpecRow> Rows);

public sealed partial class HardwareViewModel : PageViewModel
{
    private readonly MainViewModel _main;
    public HardwareViewModel(MainViewModel main) : base("Hardware", "", "Detailed specs of this PC, read from WMI and the registry.") => _main = main;

    [ObservableProperty] private IReadOnlyList<SpecGroup> _groups = Array.Empty<SpecGroup>();
    [ObservableProperty] private bool _loading = true;
    [ObservableProperty] private IReadOnlyList<string> _notes = Array.Empty<string>();

    protected override async Task LoadAsync()
    {
        var d = await Task.Run(() => _main.S.SystemInfo.Details());
        var p = _main.S.Profile;
        Groups = new[]
        {
            new SpecGroup("Processor", "", new[]
            {
                new SpecRow("Name", d.CpuName), new SpecRow("Cores / threads", $"{p.CpuCores} / {p.CpuThreads}"),
                new SpecRow("Max clock (base)", $"{d.CpuMaxClockMhz} MHz"), new SpecRow("Socket", d.CpuSocket),
                new SpecRow("L2 / L3 cache", $"{d.CpuL2Kb / 1024.0:0.#} MB / {d.CpuL3Kb / 1024.0:0.#} MB"),
                new SpecRow("Integrated graphics", p.CpuHasIntegratedGpu ? "Yes" : "No"),
            }),
            new SpecGroup("Graphics", "", d.Gpus.SelectMany(g => new[]
            {
                new SpecRow("Name", g.Name), new SpecRow("Vendor", g.Vendor.ToString()),
                new SpecRow("Memory", g.VramGb > 0 ? $"{g.VramGb:0.#} GB" : "unknown"), new SpecRow("Driver", g.DriverVersion),
            }).ToList()),
            new SpecGroup("Memory", "", new[] { new SpecRow("Total", $"{p.TotalRamGb:0.#} GB") }
                .Concat(d.Memory.Select(m => new SpecRow(m.Slot, $"{m.SizeGb:0} GB · {m.SpeedMhz} MT/s · {m.Manufacturer} {m.PartNumber}".Trim()))).ToList()),
            new SpecGroup("Storage", "", d.Disks.Select(x => new SpecRow(x.Model, $"{x.SizeGb:0} GB · {x.MediaType} · {x.BusType}")).ToList()),
            new SpecGroup("System", "", new[]
            {
                new SpecRow("Motherboard", d.Motherboard), new SpecRow("BIOS", d.Bios), new SpecRow("OS", d.Os),
                new SpecRow("Edition", p.Edition), new SpecRow("Form factor", p.IsLaptop ? "Laptop" : "Desktop"),
            }),
        };
        Notes = BuildNotes(d);
        Loading = false;
    }

    /// <summary>Plain-language hints based on what was detected.</summary>
    private List<string> BuildNotes(HardwareDetails d)
    {
        var p = _main.S.Profile;
        var notes = new List<string>();
        if (d.Memory.Count == 1)
            notes.Add("Only one memory stick is installed, so RAM runs in single-channel mode. Adding a matching second stick gives dual-channel bandwidth, often +10–20% FPS on CPU-limited games.");
        if (d.Memory.Count > 0 && d.Memory.All(m => m.SpeedMhz is > 0 and <= 2400) && p.CpuName.Contains("Intel"))
            notes.Add($"RAM is running at {d.Memory[0].SpeedMhz} MT/s. If your sticks are rated higher, enable XMP in the BIOS. On Intel B360/B365/H310 boards memory is capped at the CPU's official speed (DDR4-2400 for 9th-gen Core i3), so this may already be the maximum.");
        if (!p.CpuHasIntegratedGpu && p.CpuName.Contains("Intel"))
            notes.Add("Your CPU is an \"F\" model with no integrated graphics, so the monitor must be plugged into the graphics card.");
        if (p.CpuThreads <= 4)
            notes.Add($"With {p.CpuThreads} threads, modern games are often CPU-limited. Close background apps (Processes page) and keep startup apps to a minimum.");
        if (p.HasGpu(Core.Model.GpuVendor.Nvidia))
            notes.Add("NVIDIA: in the NVIDIA Control Panel, set Low Latency Mode to On and Power management mode to Prefer maximum performance for your games. frtune doesn't change driver settings.");
        return notes;
    }
}
