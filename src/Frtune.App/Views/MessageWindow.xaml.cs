using System.Windows;

namespace Frtune.App.Views;

public partial class MessageWindow : Window
{
    public MessageWindow(string title, string message, string ok, string? cancel, bool danger)
    {
        InitializeComponent();
        Title = title;
        Heading.Text = title;
        Body.Text = message;
        OkButton.Content = ok;
        if (danger) OkButton.Style = (Style)FindResource("DangerButton");
        if (cancel is null) CancelButton.Visibility = Visibility.Collapsed;
        else CancelButton.Content = cancel;
    }

    private void Ok_Click(object sender, RoutedEventArgs e) => DialogResult = true;
    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
