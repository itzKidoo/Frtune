namespace Frtune.Core.Tools;

public sealed record AppxPackage(string Name, string FullName, string Publisher, bool IsFramework, bool NonRemovable);

/// <summary>Which preinstalled Store apps are suggested for removal, and which are always protected.</summary>
public static class AppxCatalog
{
    /// <summary>Never removed, and the checkbox is locked. Prefix match on package name.</summary>
    public static readonly string[] Protected =
    {
        "Microsoft.WindowsStore", "Microsoft.StorePurchaseApp", "Microsoft.WindowsCalculator", "Microsoft.Windows.Photos",
        "Microsoft.WindowsTerminal", "Microsoft.DesktopAppInstaller", "Microsoft.SecHealthUI", "Microsoft.WindowsNotepad",
        "Microsoft.VCLibs", "Microsoft.NET", "Microsoft.UI.Xaml", "Microsoft.Services.Store", "Microsoft.HEIFImageExtension",
        "Microsoft.VP9VideoExtensions", "Microsoft.WebpImageExtension", "Microsoft.HEVCVideoExtension", "Microsoft.AV1VideoExtension",
        "Microsoft.WebMediaExtensions", "Microsoft.RawImageExtension", "Microsoft.ScreenSketch", "Microsoft.Paint",
        "Microsoft.XboxIdentityProvider", "Microsoft.Xbox.TCUI", "Microsoft.GamingApp", "Microsoft.GamingServices",
        "NVIDIACorp.NVIDIAControlPanel", "AdvancedMicroDevicesInc", "AppUp.IntelGraphicsExperience", "RealtekSemiconductorCorp",
        "Microsoft.Windows.ShellExperienceHost", "Microsoft.Windows.StartMenuExperienceHost", "Microsoft.AAD", "Microsoft.AccountsControl",
        "Microsoft.LockApp", "Microsoft.Windows.CloudExperienceHost", "windows.immersivecontrolpanel", "MicrosoftWindows.Client",
    };

    /// <summary>Suggested (pre-ticked) for removal. Everything else is listed but unticked.</summary>
    public static readonly Dictionary<string, string> Suggested = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Microsoft.MicrosoftSolitaireCollection"] = "Solitaire Collection",
        ["Clipchamp.Clipchamp"] = "Clipchamp video editor",
        ["Microsoft.BingNews"] = "News",
        ["Microsoft.BingWeather"] = "Weather",
        ["Microsoft.BingSearch"] = "Bing Search",
        ["Microsoft.WindowsMaps"] = "Maps",
        ["MicrosoftTeams"] = "Teams (personal)",
        ["MSTeams"] = "Teams (personal)",
        ["Microsoft.SkypeApp"] = "Skype",
        ["Microsoft.MixedReality.Portal"] = "Mixed Reality Portal",
        ["Microsoft.GetHelp"] = "Get Help",
        ["Microsoft.Getstarted"] = "Tips",
        ["Microsoft.People"] = "People",
        ["Microsoft.WindowsFeedbackHub"] = "Feedback Hub",
        ["Microsoft.MicrosoftOfficeHub"] = "Microsoft 365 (Office) hub",
        ["Microsoft.Todos"] = "To Do",
        ["Microsoft.PowerAutomateDesktop"] = "Power Automate",
        ["Microsoft.ZuneVideo"] = "Movies & TV",
        ["Microsoft.549981C3F5F10"] = "Cortana",
        ["Microsoft.Microsoft3DViewer"] = "3D Viewer",
        ["Microsoft.Office.OneNote"] = "OneNote for Windows 10",
        ["Microsoft.OutlookForWindows"] = "Outlook (new)",
        ["Microsoft.WindowsAlarms"] = "Clock",
        ["Microsoft.YourPhone"] = "Phone Link",
        ["Microsoft.Copilot"] = "Copilot",
        ["Microsoft.XboxApp"] = "Xbox Console Companion",
        ["Microsoft.XboxSpeechToTextOverlay"] = "Xbox speech-to-text",
        ["king.com.CandyCrushSaga"] = "Candy Crush Saga",
        ["king.com.CandyCrushSodaSaga"] = "Candy Crush Soda Saga",
        ["SpotifyAB.SpotifyMusic"] = "Spotify (preinstalled)",
        ["Disney.37853FC22B2CE"] = "Disney+",
        ["BytedancePte.Ltd.TikTok"] = "TikTok",
        ["Facebook.Facebook"] = "Facebook",
        ["AmazonVideo.PrimeVideo"] = "Prime Video",
    };

    public static bool IsProtected(string name) =>
        Protected.Any(p => name.StartsWith(p, StringComparison.OrdinalIgnoreCase));

    public static string? FriendlyName(string name) => Suggested.GetValueOrDefault(name);
}
