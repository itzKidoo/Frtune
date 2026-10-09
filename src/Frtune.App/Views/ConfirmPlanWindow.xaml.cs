using System.Windows;
using Frtune.Core.Engine;
using Frtune.Core.Model;

namespace Frtune.App.Views;

/// <summary>Lists every change (registry value, service, command) before anything runs.</summary>
public partial class ConfirmPlanWindow : Window
{
    private sealed record Row(string Verb, string Name, string Risk, bool Enable, string? Warning, string? Problem, string Lines, bool Skipped);

    private readonly bool _needsConsent;

    public ConfirmPlanWindow(IReadOnlyList<PlanItem> plan, bool createsRestorePoint)
    {
        InitializeComponent();
        var rows = plan.Select(p => new Row(
            p.Enable ? "Apply" : "Revert",
            p.Tweak.Name,
            p.Tweak.Risk.ToString(),
            p.Enable,
            p.Enable ? p.Tweak.Warning : null,
            p.Problem is null ? null : "Will be skipped: " + p.Problem,
            string.Join("\n", p.Lines.Select(l => "• " + l)),
            !p.CanRun)).ToList();
        Items.ItemsSource = rows;

        var apply = plan.Count(p => p.Enable && p.CanRun);
        var revert = plan.Count(p => !p.Enable && p.CanRun);
        Heading.Text = $"Review {apply + revert} change(s)";
        SubHeading.Text = $"{apply} to apply, {revert} to revert. Below is exactly what will be written.";
        Footer.Text = createsRestorePoint
            ? "A System Restore point and a backup of every value above are created first."
            : "A backup of every value above is saved first (restore point is off in Settings).";
        if (apply + revert == 0)
        {
            OkButton.IsEnabled = false;
            Footer.Text = "Nothing can be applied.";
        }

        _needsConsent = plan.Any(p => p.Enable && p.CanRun && (p.Tweak.Risk == TweakRisk.Advanced || p.Tweak.Warning is not null));
        if (_needsConsent)
        {
            Understand.Visibility = Visibility.Visible;
            OkButton.IsEnabled = false;
        }
    }

    private void Understand_Changed(object sender, RoutedEventArgs e) => OkButton.IsEnabled = Understand.IsChecked == true;
    private void Ok_Click(object sender, RoutedEventArgs e) => DialogResult = true;
    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
