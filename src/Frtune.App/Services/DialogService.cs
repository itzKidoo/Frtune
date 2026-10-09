using System.Windows;
using Frtune.App.Views;
using Frtune.Core.Engine;

namespace Frtune.App.Services;

public interface IDialogService
{
    /// <summary>Shows exactly what will change. Returns false if the user cancels.</summary>
    bool ConfirmPlan(IReadOnlyList<PlanItem> plan, bool createsRestorePoint);
    bool Ask(string title, string message, string okText = "Continue", bool danger = false);
    void Info(string title, string message);
    string? PickFile(string filter);
    string? SaveFile(string filter, string defaultName);
    string? PickFolder();
}

public sealed class DialogService : IDialogService
{
    private static Window? Owner => Application.Current.MainWindow is { IsLoaded: true } w ? w : null;

    public bool ConfirmPlan(IReadOnlyList<PlanItem> plan, bool createsRestorePoint)
    {
        var w = new ConfirmPlanWindow(plan, createsRestorePoint) { Owner = Owner };
        return w.ShowDialog() == true;
    }

    public bool Ask(string title, string message, string okText = "Continue", bool danger = false)
    {
        var w = new MessageWindow(title, message, okText, "Cancel", danger) { Owner = Owner };
        return w.ShowDialog() == true;
    }

    public void Info(string title, string message)
    {
        var w = new MessageWindow(title, message, "OK", null, false) { Owner = Owner };
        w.ShowDialog();
    }

    public string? PickFile(string filter)
    {
        var d = new Microsoft.Win32.OpenFileDialog { Filter = filter };
        return d.ShowDialog(Owner) == true ? d.FileName : null;
    }

    public string? SaveFile(string filter, string defaultName)
    {
        var d = new Microsoft.Win32.SaveFileDialog { Filter = filter, FileName = defaultName };
        return d.ShowDialog(Owner) == true ? d.FileName : null;
    }

    public string? PickFolder()
    {
        var d = new Microsoft.Win32.OpenFolderDialog();
        return d.ShowDialog(Owner) == true ? d.FolderName : null;
    }
}
