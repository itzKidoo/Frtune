using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using Frtune.App.ViewModels;

namespace Frtune.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => ApplyWindowFrame();
        StateChanged += (_, _) => OnStateChanged();
        DataContextChanged += (_, e) =>
        {
            if (e.OldValue is INotifyPropertyChanged o) o.PropertyChanged -= OnVmChanged;
            if (e.NewValue is INotifyPropertyChanged n) n.PropertyChanged += OnVmChanged;
        };
        InputBindings.Add(new KeyBinding(new RelayKey(() => { SearchBox.Focus(); SearchBox.SelectAll(); }), Key.F, ModifierKeys.Control));
        InputBindings.Add(new KeyBinding(new RelayKey(() => { if (DataContext is MainViewModel vm) vm.SearchText = ""; }), Key.Escape, ModifierKeys.None));
    }

    /// <summary>Fade + slide in whenever the page changes.</summary>
    private void OnVmChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainViewModel.CurrentPage)) return;
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        PageHost.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)));
        PageShift.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty,
            new DoubleAnimation(10, 0, TimeSpan.FromMilliseconds(220)) { EasingFunction = ease });
    }

    private void OnStateChanged()
    {
        // A maximised chrome-less window overhangs the screen by the resize border; pad it back in.
        Root.Margin = WindowState == WindowState.Maximized ? new Thickness(7) : new Thickness(0);
        MaxButton.Content = WindowState == WindowState.Maximized ? "" : "";
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void Maximize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    /// <summary>Rounded corners and a dark frame on Windows 11 (ignored on Windows 10).</summary>
    private void ApplyWindowFrame()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        int dark = 1, round = 2; // DWMWCP_ROUND
        _ = DwmSetWindowAttribute(hwnd, 20, ref dark, sizeof(int));  // DWMWA_USE_IMMERSIVE_DARK_MODE
        _ = DwmSetWindowAttribute(hwnd, 33, ref round, sizeof(int)); // DWMWA_WINDOW_CORNER_PREFERENCE
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    private sealed class RelayKey : ICommand
    {
        private readonly Action _a;
        public RelayKey(Action a) => _a = a;
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? p) => true;
        public void Execute(object? p) => _a();
    }
}
