using Frtune.Core.Abstractions;
using Frtune.Core.Model;
using static Frtune.Core.Model.TweakCategory;
using static Frtune.Core.Model.TweakRisk;

namespace Frtune.Core.Tweaks.Catalog;

/// <summary>Safe-to-disable service presets. The full service list lives on the Services page.</summary>
internal static class ServiceTweaks
{
    public static IEnumerable<Tweak> All()
    {
        Tweak Svc(string id, string name, string desc, TweakRisk risk, string service, ServiceStartType type = ServiceStartType.Disabled) =>
            new Tweak("svc." + id, name, desc, Services, risk).Service(service, type);

        yield return Svc("spooler", "Disable Print Spooler", "No printer was detected. The spooler has a history of security bugs (PrintNightmare). Turn it back on before adding a printer.", Safe, "Spooler")
            .When(s => !s.HasPhysicalPrinter, "Only when no physical printer is installed")
            .In(Preset.LowEnd | Preset.Gaming | Preset.MaxPerformance);
        yield return Svc("fax", "Disable Fax", "The Fax service. Nobody needs it on a home PC.", Safe, "Fax")
            .In(Preset.LowEnd | Preset.Balanced | Preset.Gaming | Preset.MaxPerformance);
        yield return Svc("remoteregistry", "Disable Remote Registry", "Stops other computers editing this PC's registry over the network.", Safe, "RemoteRegistry")
            .In(Preset.LowEnd | Preset.Balanced | Preset.Privacy | Preset.Gaming | Preset.MaxPerformance);
        yield return Svc("bluetooth", "Disable Bluetooth support", "No Bluetooth adapter was detected, so the Bluetooth Support Service has nothing to do.", Safe, "bthserv")
            .When(s => !s.HasBluetooth, "Only when no Bluetooth adapter is present");
        yield return Svc("retaildemo", "Disable Retail Demo", "Only used on store display machines.", Safe, "RetailDemo")
            .In(Preset.LowEnd | Preset.Balanced | Preset.Gaming | Preset.MaxPerformance);
        yield return Svc("maps", "Disable Downloaded Maps Manager", "Only the Maps app uses it.", Safe, "MapsBroker")
            .In(Preset.LowEnd | Preset.Gaming | Preset.MaxPerformance);
        yield return Svc("geolocation", "Disable Geolocation service", "Apps can no longer get your location. Weather and Maps fall back to manual locations.", Moderate, "lfsvc")
            .When(s => !s.IsLaptop, "Hidden on laptops").In(Preset.Privacy);
        yield return Svc("wer", "Set Error Reporting to manual", "Windows Error Reporting starts only when a crash happens instead of idling in the background.", Safe, "WerSvc", ServiceStartType.Manual)
            .In(Preset.LowEnd | Preset.Privacy);
        yield return Svc("phone", "Set Phone Service to manual", "Telephony state for Phone Link. Starts on demand instead of at boot.", Safe, "PhoneSvc", ServiceStartType.Manual)
            .In(Preset.LowEnd);
        yield return Svc("tablet", "Disable touch keyboard and handwriting", "Not needed on a desktop without a touch screen or pen.", Moderate, "TabletInputService")
            .When(s => !s.IsLaptop, "Desktops only");
    }
}
