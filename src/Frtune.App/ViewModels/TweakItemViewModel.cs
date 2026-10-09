using CommunityToolkit.Mvvm.ComponentModel;
using Frtune.Core.Engine;
using Frtune.Core.Model;
using Frtune.Core.Tweaks;

namespace Frtune.App.ViewModels;

/// <summary>
/// One tweak card. <see cref="IsOn"/> is what the user wants; <see cref="Status"/> is what the
/// system actually is. A difference between the two is a pending change.
/// </summary>
public sealed partial class TweakItemViewModel : ObservableObject
{
    private readonly Action _pendingChanged;

    public TweakItemViewModel(ITweak tweak, SystemProfile system, Action pendingChanged)
    {
        Tweak = tweak;
        _pendingChanged = pendingChanged;
        IsApplicable = tweak.IsApplicable(system);
        Recommendation = tweak.RecommendationFor(system);
        Details = BuildDetails(tweak);
    }

    public ITweak Tweak { get; }
    public string Name => Tweak.Name;
    public string Description => Tweak.Description;
    public string Risk => Tweak.Risk.ToString();
    public string Category => Tweak.Category.ToString();
    public bool IsApplicable { get; }
    public string? Recommendation { get; }
    public bool IsRecommended => Recommendation is not null;
    public string Details { get; }
    public string? NotApplicableReason => IsApplicable ? null : (Tweak as Tweak)?.ApplicabilityNote;

    public string RestartText => Tweak.Restart switch
    {
        RestartKind.Reboot => "Restart",
        RestartKind.SignOut => "Sign out",
        RestartKind.ExplorerRestart => "Explorer restart",
        _ => "",
    };
    public bool NeedsRestart => Tweak.Restart != RestartKind.None;

    [ObservableProperty] private bool _isOn;
    [ObservableProperty] private bool _isChecking = true;
    [ObservableProperty] private TweakStatus _status = TweakStatus.Off;

    public bool ActuallyOn => Status is TweakStatus.On or TweakStatus.AlreadySet or TweakStatus.Partial;
    public bool IsPending => !IsChecking && IsApplicable && IsOn != ActuallyOn;
    /// <summary>Toggling off is impossible without a backup, so the switch is locked.</summary>
    public bool CanToggle => !IsChecking && IsApplicable && Status != TweakStatus.AlreadySet && Status != TweakStatus.Error;

    public string StatusText => Status switch
    {
        _ when IsChecking => "Checking…",
        TweakStatus.AlreadySet => "Already set on this PC",
        TweakStatus.Partial => "Partly undone by Windows. Turn off to restore, or re-apply.",
        TweakStatus.Error => "Couldn't read the current state",
        TweakStatus.On => "Applied by frtune",
        _ => "",
    };

    partial void OnIsOnChanged(bool value) => NotifyPending();

    partial void OnStatusChanged(TweakStatus value)
    {
        OnPropertyChanged(nameof(ActuallyOn));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(CanToggle));
    }

    partial void OnIsCheckingChanged(bool value)
    {
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(CanToggle));
        NotifyPending();
    }

    /// <summary>Sets the real state and resets the toggle to match it (clears any pending change).</summary>
    public void SetActual(TweakStatus status)
    {
        Status = status;
        IsChecking = false;
        IsOn = ActuallyOn;
        NotifyPending();
    }

    public void DiscardPending() => IsOn = ActuallyOn;

    private void NotifyPending()
    {
        OnPropertyChanged(nameof(IsPending));
        _pendingChanged();
    }

    public bool Matches(string query) =>
        Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
        Description.Contains(query, StringComparison.OrdinalIgnoreCase) ||
        Tweak.Id.Contains(query, StringComparison.OrdinalIgnoreCase) ||
        Details.Contains(query, StringComparison.OrdinalIgnoreCase);

    private static string BuildDetails(ITweak t)
    {
        var lines = new List<string>();
        if (t is Tweak tw)
        {
            lines.AddRange(tw.StaticChanges.Select(c => "• " + c.Describe()));
            if (tw.HasDynamicChanges) lines.Add("• Plus per-adapter values on this PC (listed in the confirmation dialog)");
        }
        if (t.Restart != RestartKind.None) lines.Add($"Takes effect after: {t.Restart}");
        lines.Add("Id: " + t.Id);
        return string.Join("\n", lines);
    }
}
