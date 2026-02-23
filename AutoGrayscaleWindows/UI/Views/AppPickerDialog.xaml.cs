using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace AutoGrayscaleWindows.UI.Views;

/// <summary>
/// Модель приложения для отображения в списке
/// </summary>
public class AppInfo
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public BitmapSource? Icon { get; set; }

    public override string ToString() => Name;
}

/// <summary>
/// Interaction logic for AppPickerDialog.xaml
/// </summary>
public partial class AppPickerDialog : Window
{
    private ObservableCollection<AppInfo> _allApplications = new();
    private ObservableCollection<AppInfo> _filteredApplications = new();
    
    public AppInfo? SelectedApplication { get; private set; }
    
    public string? SelectedPath => SelectedApplication?.Path;
    public string? SelectedName => SelectedApplication?.Name;

    public AppPickerDialog()
    {
        InitializeComponent();
        ApplicationsListView.ItemsSource = _filteredApplications;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        LoadRunningApplications();
    }

    private void LoadRunningApplications()
    {
        LoadingText.Text = "Загрузка запущенных приложений...";
        
        Task.Run(() =>
        {
            var apps = new List<AppInfo>();
            var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                foreach (var process in Process.GetProcesses())
                {
                    try
                    {
                        if (process.MainWindowHandle != nint.Zero && 
                            !string.IsNullOrEmpty(process.MainModule?.FileName))
                        {
                            var path = process.MainModule.FileName;
                            
                            // Пропускаем системные процессы и дубликаты
                            if (seenPaths.Contains(path))
                                continue;
                            
                            if (path.StartsWith(Environment.SystemDirectory, StringComparison.OrdinalIgnoreCase) ||
                                path.StartsWith(Environment.GetFolderPath(Environment.SpecialFolder.Windows), StringComparison.OrdinalIgnoreCase))
                                continue;

                            seenPaths.Add(path);

                            var appInfo = new AppInfo
                            {
                                Name = process.ProcessName,
                                Path = path
                            };

                            // Получаем иконку
                            try
                            {
                                var icon = System.Drawing.Icon.ExtractAssociatedIcon(path);
                                if (icon != null)
                                {
                                    var bitmap = icon.ToBitmap();
                                    var hBitmap = bitmap.GetHbitmap();
                                    
                                    appInfo.Icon = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                                        hBitmap, nint.Zero, Int32Rect.Empty,
                                        BitmapSizeOptions.FromEmptyOptions());
                                    
                                    DeleteObject(hBitmap);
                                }
                            }
                            catch
                            {
                                // Игнорируем ошибки при получении иконки
                            }

                            apps.Add(appInfo);
                        }
                    }
                    catch (Exception)
                    {
                        // Игнорируем ошибки доступа к процессу
                    }
                    finally
                    {
                        process.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    LoadingText.Text = $"Ошибка: {ex.Message}";
                });
                return;
            }

            Dispatcher.Invoke(() =>
            {
                _allApplications.Clear();
                foreach (var app in apps.OrderBy(a => a.Name))
                {
                    _allApplications.Add(app);
                }
                
                _filteredApplications.Clear();
                foreach (var app in _allApplications)
                {
                    _filteredApplications.Add(app);
                }

                LoadingText.Text = _allApplications.Count > 0 
                    ? $"Найдено {_allApplications.Count} приложений"
                    : "Запущенные приложения не найдены";
            });
        });
    }

    [System.Runtime.InteropServices.DllImport("gdi32.dll")]
    private static extern bool DeleteObject(nint hObject);

    private void SearchTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        var search = SearchTextBox.Text.Trim().ToLower();
        
        SearchPlaceholder.Visibility = string.IsNullOrEmpty(search) 
            ? Visibility.Visible 
            : Visibility.Collapsed;

        _filteredApplications.Clear();

        foreach (var app in _allApplications)
        {
            if (string.IsNullOrEmpty(search) ||
                app.Name.ToLower().Contains(search) ||
                app.Path.ToLower().Contains(search))
            {
                _filteredApplications.Add(app);
            }
        }
    }

    private void ApplicationsListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ApplicationsListView.SelectedItem is AppInfo appInfo)
        {
            SelectApplication(appInfo);
        }
    }

    private void SelectButton_Click(object sender, RoutedEventArgs e)
    {
        if (ApplicationsListView.SelectedItem is AppInfo appInfo)
        {
            SelectApplication(appInfo);
        }
        else
        {
            MessageBox.Show("Выберите приложение из списка", 
                "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void SelectApplication(AppInfo appInfo)
    {
        SelectedApplication = appInfo;
        DialogResult = true;
        Close();
    }

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Выберите приложение",
            Filter = "Исполняемые файлы (*.exe)|*.exe|Все файлы (*.*)|*.*",
            CheckFileExists = true,
            CheckPathExists = true
        };

        if (dialog.ShowDialog() == true)
        {
            var appInfo = new AppInfo
            {
                Name = Path.GetFileNameWithoutExtension(dialog.FileName),
                Path = dialog.FileName
            };

            // Получаем иконку
            try
            {
                var icon = System.Drawing.Icon.ExtractAssociatedIcon(dialog.FileName);
                if (icon != null)
                {
                    var bitmap = icon.ToBitmap();
                    var hBitmap = bitmap.GetHbitmap();
                    
                    appInfo.Icon = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                        hBitmap, nint.Zero, Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());
                    
                    DeleteObject(hBitmap);
                }
            }
            catch
            {
                // Игнорируем ошибки
            }

            SelectedApplication = appInfo;
            DialogResult = true;
            Close();
        }
    }
}
