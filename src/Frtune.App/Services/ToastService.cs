using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;

namespace Frtune.App.Services;

public enum ToastKind { Success, Error, Warning, Info, Restart }

public sealed record Toast(ToastKind Kind, string Title, string Message)
{
    public string Glyph => Kind switch
    {
        ToastKind.Success => "",
        ToastKind.Error => "",
        ToastKind.Warning => "",
        ToastKind.Restart => "",
        _ => "",
    };
}

/// <summary>Bottom-right notifications. They disappear on their own; restart toasts stay until dismissed.</summary>
public sealed class ToastService
{
    public ObservableCollection<Toast> Items { get; } = new();

    public void Show(ToastKind kind, string title, string message = "")
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            var toast = new Toast(kind, title, message);
            Items.Add(toast);
            while (Items.Count > 4) Items.RemoveAt(0);
            if (kind == ToastKind.Restart) return;
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(kind == ToastKind.Error ? 9 : 5) };
            timer.Tick += (_, _) => { timer.Stop(); Items.Remove(toast); };
            timer.Start();
        });
    }

    public void Dismiss(Toast t) => Items.Remove(t);
}
