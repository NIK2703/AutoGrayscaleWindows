using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using AutoGrayscaleWindows.Core;
using AutoGrayscaleWindows.Models;
using AutoGrayscaleWindows.Services;
using Microsoft.Win32;
using Serilog;
using Wpf.Ui.Controls;
using System.Collections.ObjectModel;

namespace AutoGrayscaleWindows.UI.Views;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : FluentWindow
{
    private bool _minimizeToTray = true;
    private ConfigManager? _configManager;
    private StartupManager? _startupManager;
    private ObservableCollection<AppRule> _currentRules;
    private AppConfig _config;
    private bool _isLoading = true;

    public int RulesCount => _currentRules.Count;

    /// <summary>
    /// Событие при смене режима списка (цветные/серые)
    /// </summary>
    public event EventHandler? ListModeChanged;

    public MainWindow()
    {
        InitializeComponent();
        _config = new AppConfig();
        _currentRules = new ObservableCollection<AppRule>();
        DataContext = this;
    }

    public void Initialize(
        FilterController filterController,
        WindowMonitor windowMonitor,
        ConfigManager configManager,
        StartupManager startupManager)
    {
        _configManager = configManager;
        _startupManager = startupManager;
        _config = configManager.Config ?? new AppConfig();
        
        // Загружаем текущий список правил и устанавливаем MatchType.Contains для всех
        _currentRules = new ObservableCollection<AppRule>(_config.GetCurrentRules().Select(r =>
        {
            var clone = r.Clone();
            clone.MatchType = MatchType.Contains; // Всегда используем "Содержит"
            return clone;
        }));

        InitializeControls();
        AttachEventHandlers();
        _isLoading = false;
    }

    private void InitializeControls()
    {
        EnableAppCheckBox.IsChecked = _config.IsEnabled;
        AutoStartCheckBox.IsChecked = _config.AutoStart;
        MinimizeToTrayCheckBox.IsChecked = _config.MinimizeToTray;
        
        // Устанавливаем RadioButton в зависимости от режима
        // UseWhitelist = true = список серых (GrayListRadio)
        // UseWhitelist = false = список цветных (ColorListRadio)
        if (_config.UseWhitelist)
        {
            GrayListRadio.IsChecked = true;
        }
        else
        {
            ColorListRadio.IsChecked = true;
        }

        // Устанавливаем язык
        SetLanguageComboBox(_config.Language);

        RulesDataGrid.ItemsSource = _currentRules;
    }

    private void SetLanguageComboBox(string languageCode)
    {
        switch (languageCode)
        {
            case "en":
                LanguageComboBox.SelectedIndex = 1;
                break;
            case "ru":
            default:
                LanguageComboBox.SelectedIndex = 0;
                break;
        }
    }

    private void AttachEventHandlers()
    {
        AddRuleButton.Click += AddRuleButton_Click;
    }

    // Автоматическое сохранение настроек
    private void Setting_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;
        SaveConfiguration();
    }

    private void ListModeRadio_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;
        
        // Сохраняем текущие правила в соответствующий список
        SaveCurrentRulesToList();
        
        // Переключаем режим
        // GrayListRadio.IsChecked = true = Whitelist (список серых)
        // ColorListRadio.IsChecked = true = Blacklist (список цветных)
        _config.UseWhitelist = GrayListRadio.IsChecked == true;
        
        // Загружаем правила нового режима и устанавливаем MatchType.Contains для всех
        _currentRules = new ObservableCollection<AppRule>(_config.GetCurrentRules().Select(r =>
        {
            var clone = r.Clone();
            clone.MatchType = MatchType.Contains; // Всегда используем "Содержит"
            return clone;
        }));
        RulesDataGrid.ItemsSource = _currentRules;
        OnPropertyChanged(nameof(RulesCount));
        
        SaveConfiguration();
        
        // Уведомляем о смене режима для немедленного применения правил
        ListModeChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RulesDataGrid_CurrentCellChanged(object? sender, EventArgs e)
    {
        if (_isLoading) return;
        // Сохранение при смене текущей ячейки
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() =>
        {
            SaveConfiguration();
        }));
    }

    // Обработчики событий для элементов таблицы
    private void RuleCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;
        SaveConfiguration();
    }

    private void RuleTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isLoading) return;
        // Отложенное сохранение для текстовых полей
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() =>
        {
            SaveConfiguration();
        }));
    }

    private void RuleComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading) return;
        SaveConfiguration();
    }

    private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading) return;
        
        if (LanguageComboBox.SelectedItem is ComboBoxItem selectedItem)
        {
            var languageCode = selectedItem.Tag as string ?? "ru";
            
            // Сохраняем язык в конфигурации
            _config.Language = languageCode;
            
            // Применяем язык
            LocalizationManager.Instance.SetLanguage(languageCode);
            
            // Обновляем локализацию в трее
            App.Current.Dispatcher.Invoke(() =>
            {
                (App.Current as App)?.UpdateTrayLocalization();
            });
            
            // Обновляем заголовки столбцов DataGrid
            UpdateDataGridHeaders();
            
            // Обновляем контекстное меню
            UpdateContextMenu();
            
            SaveConfiguration();
            
            Log.Debug("Язык изменён на: {Language}", languageCode);
        }
    }

    private void UpdateDataGridHeaders()
    {
        // Получаем локализованные строки
        var columnActive = LocalizationManager.GetString("ColumnActive");
        var columnPattern = LocalizationManager.GetString("ColumnPattern");
        var columnMode = LocalizationManager.GetString("ColumnMode");
        var executableFile = LocalizationManager.GetString("ExecutableFile");
        var windowTitle = LocalizationManager.GetString("WindowTitle");
        var browseApp = LocalizationManager.GetString("BrowseApp");
        var deleteRule = LocalizationManager.GetString("DeleteRule");
        
        // Обновляем заголовки столбцов
        if (RulesDataGrid.Columns.Count >= 4)
        {
            RulesDataGrid.Columns[0].Header = columnActive;
            RulesDataGrid.Columns[1].Header = columnPattern;
            RulesDataGrid.Columns[2].Header = columnMode;
        }
        
        // Обновляем элементы ComboBox в столбце режима
        if (RulesDataGrid.Columns.Count >= 3 && RulesDataGrid.Columns[2] is DataGridTemplateColumn modeColumn)
        {
            // Обновляем шаблон ячейки с новыми значениями ComboBox
            var cellTemplate = new DataTemplate();
            var factory = new FrameworkElementFactory(typeof(ComboBox));
            factory.SetBinding(ComboBox.SelectedIndexProperty, new System.Windows.Data.Binding("MatchTarget")
            {
                Converter = new EnumToIndexConverter(),
                UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.PropertyChanged,
                Mode = System.Windows.Data.BindingMode.TwoWay
            });
            factory.SetValue(ComboBox.VerticalAlignmentProperty, VerticalAlignment.Center);
            factory.SetValue(ComboBox.BorderThicknessProperty, new Thickness(0));
            factory.SetValue(ComboBox.BackgroundProperty, System.Windows.Media.Brushes.Transparent);
            factory.AddHandler(ComboBox.SelectionChangedEvent, new SelectionChangedEventHandler(RuleComboBox_SelectionChanged));
            
            var comboBox = new ComboBox();
            comboBox.Items.Add(new ComboBoxItem { Content = executableFile });
            comboBox.Items.Add(new ComboBoxItem { Content = windowTitle });
            
            factory.SetValue(ComboBox.ItemsSourceProperty, new[] { executableFile, windowTitle });
            
            cellTemplate.VisualTree = factory;
            modeColumn.CellTemplate = cellTemplate;
        }
        
        // Обновляем кнопку обзора и удаления
        RulesDataGrid.Items.Refresh();
    }

    private void UpdateContextMenu()
    {
        // Получаем локализованные строки
        var duplicate = LocalizationManager.GetString("Duplicate");
        var toggleEnable = LocalizationManager.GetString("ToggleEnable");
        var delete = LocalizationManager.GetString("Delete");
        
        // Создаём новое контекстное меню
        var contextMenu = new ContextMenu();
        
        var duplicateItem = new System.Windows.Controls.MenuItem
        {
            Header = duplicate,
            Icon = new Wpf.Ui.Controls.SymbolIcon { Symbol = Wpf.Ui.Controls.SymbolRegular.Copy24 }
        };
        duplicateItem.Click += DuplicateRuleContextMenu_Click;
        contextMenu.Items.Add(duplicateItem);
        
        contextMenu.Items.Add(new Separator());
        
        var toggleItem = new System.Windows.Controls.MenuItem
        {
            Header = toggleEnable,
            Icon = new Wpf.Ui.Controls.SymbolIcon { Symbol = Wpf.Ui.Controls.SymbolRegular.ToggleLeft24 }
        };
        toggleItem.Click += ToggleRuleContextMenu_Click;
        contextMenu.Items.Add(toggleItem);
        
        contextMenu.Items.Add(new Separator());
        
        var deleteItem = new System.Windows.Controls.MenuItem
        {
            Header = delete,
            Icon = new Wpf.Ui.Controls.SymbolIcon
            {
                Symbol = Wpf.Ui.Controls.SymbolRegular.Delete24,
                Foreground = System.Windows.Media.Brushes.Red
            }
        };
        deleteItem.Click += DeleteRuleContextMenu_Click;
        contextMenu.Items.Add(deleteItem);
        
        RulesDataGrid.ContextMenu = contextMenu;
    }

    private void SaveCurrentRulesToList()
    {
        if (_config.UseWhitelist)
        {
            _config.WhitelistRules = _currentRules.ToList();
        }
        else
        {
            _config.BlacklistRules = _currentRules.ToList();
        }
    }

    private void SaveConfiguration()
    {
        if (_config == null || _configManager == null) return;

        // Обновляем конфигурацию
        _config.IsEnabled = EnableAppCheckBox.IsChecked ?? true;
        _config.AutoStart = AutoStartCheckBox.IsChecked ?? false;
        _config.MinimizeToTray = MinimizeToTrayCheckBox.IsChecked ?? true;
        // UseWhitelist обновляется через ToggleSwitch

        // Сохраняем текущие правила в соответствующий список
        SaveCurrentRulesToList();

        // Сохраняем через ConfigManager
        _configManager.UpdateConfig(_config);
        
        // Обновляем автозапуск
        if (_startupManager != null)
        {
            if (_config.AutoStart)
                _startupManager.EnableAutoStart();
            else
                _startupManager.DisableAutoStart();
        }

        Log.Debug("Настройки автоматически сохранены");
    }

    private void AddRuleButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Log.Debug("AddRuleButton_Click: начало добавления правила");
            
            var newRule = new AppRule
            {
                DisplayName = "",
                AppIdentifier = "",
                IsActive = true,
                MatchType = MatchType.Contains,
                MatchTarget = MatchTarget.Executable,
                Priority = _currentRules.Count > 0 ? _currentRules.Max(r => r.Priority) + 10 : 10
            };

            Log.Debug($"AddRuleButton_Click: создано правило {newRule.DisplayName}, Priority={newRule.Priority}");
            
            _currentRules.Add(newRule);
            Log.Debug($"AddRuleButton_Click: правило добавлено в список, всего правил: {_currentRules.Count}");
            
            // ObservableCollection автоматически уведомляет DataGrid об изменениях
            // RefreshRulesList() не нужен
            
            OnPropertyChanged(nameof(RulesCount));
            Log.Debug("AddRuleButton_Click: событие PropertyChanged вызвано");
            
            SaveConfiguration();
            Log.Debug("AddRuleButton_Click: конфигурация сохранена");
            
            // Прокручиваем к новой строке
            RulesDataGrid.ScrollIntoView(newRule);
            
            Log.Debug("AddRuleButton_Click: завершено успешно");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "AddRuleButton_Click: ошибка при добавлении правила");
            System.Windows.MessageBox.Show($"Ошибка при добавлении правила: {ex.Message}\n\n{ex.StackTrace}", "Ошибка", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    private void DeleteRuleRowButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button button && button.Tag is AppRule rule)
        {
            _currentRules.Remove(rule);
            // ObservableCollection автоматически уведомляет DataGrid
            OnPropertyChanged(nameof(RulesCount));
            SaveConfiguration();
        }
    }

    // Контекстное меню
    private void DuplicateRuleContextMenu_Click(object sender, RoutedEventArgs e)
    {
        // Получаем правило из текущей ячейки
        var selectedCell = RulesDataGrid.CurrentCell;
        if (selectedCell.Item is AppRule selectedRule)
        {
            var duplicate = selectedRule.Clone();
            duplicate.DisplayName = $"{selectedRule.DisplayName} (копия)";
            duplicate.Id = Guid.NewGuid();
            duplicate.CreatedAt = DateTime.Now;
            duplicate.UpdatedAt = DateTime.Now;
            
            _currentRules.Add(duplicate);
            // ObservableCollection автоматически уведомляет DataGrid
            OnPropertyChanged(nameof(RulesCount));
            SaveConfiguration();
        }
    }

    private void ToggleRuleContextMenu_Click(object sender, RoutedEventArgs e)
    {
        // Получаем правило из текущей ячейки
        var selectedCell = RulesDataGrid.CurrentCell;
        if (selectedCell.Item is AppRule selectedRule)
        {
            selectedRule.IsActive = !selectedRule.IsActive;
            // Для обновления UI нужно обновить ItemsSource
            RulesDataGrid.Items.Refresh();
            SaveConfiguration();
        }
    }

    private void DeleteRuleContextMenu_Click(object sender, RoutedEventArgs e)
    {
        // Получаем правило из текущей ячейки
        var selectedCell = RulesDataGrid.CurrentCell;
        if (selectedCell.Item is AppRule selectedRule)
        {
            _currentRules.Remove(selectedRule);
            // ObservableCollection автоматически уведомляет DataGrid
            OnPropertyChanged(nameof(RulesCount));
            SaveConfiguration();
        }
    }

    private void BrowseAppButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button button && button.Tag is AppRule rule)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Выберите приложение",
                Filter = "Исполняемые файлы (*.exe)|*.exe|Все файлы (*.*)|*.*",
                CheckFileExists = true
            };

            if (dialog.ShowDialog() == true)
            {
                rule.AppIdentifier = dialog.FileName;
                
                // Обновляем отображение строки
                RulesDataGrid.Items.Refresh();
                SaveConfiguration();
            }
        }
    }

    private void MainWindow_OnClosing(object sender, CancelEventArgs e)
    {
        // Если нужно сворачивать в трей вместо закрытия
        if (_minimizeToTray)
        {
            e.Cancel = true;
            Hide();
        }
    }

    public void SetMinimizeToTray(bool value)
    {
        _minimizeToTray = value;
    }

    /// <summary>
    /// Обновляет состояние чекбокса мониторинга из внешнего источника
    /// </summary>
    public void UpdateMonitoringCheckBox(bool isEnabled)
    {
        _isLoading = true;
        EnableAppCheckBox.IsChecked = isEnabled;
        _config.IsEnabled = isEnabled;
        _isLoading = false;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    
    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
