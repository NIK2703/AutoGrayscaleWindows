using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;

namespace AutoGrayscaleWindows.UI.Views;

/// <summary>
/// Interaction logic for AboutWindow.xaml
/// </summary>
public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        
        // Устанавливаем версию
        var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        VersionTextBlock.Text = version != null 
            ? $"Версия {version.ToString(3)}" 
            : "Версия 1.0.0";
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = e.Uri.AbsoluteUri,
                UseShellExecute = true
            });
            e.Handled = true;
        }
        catch
        {
            // Игнорируем ошибки при открытии ссылок
        }
    }
}
