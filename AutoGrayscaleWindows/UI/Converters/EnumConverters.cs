using System.Globalization;
using System.Windows.Data;
using AutoGrayscaleWindows.Models;

namespace AutoGrayscaleWindows.UI.Converters;

/// <summary>
/// Конвертер для получения описания enum RuleAction
/// </summary>
public class RuleActionToDescriptionConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is RuleAction action)
        {
            return action switch
            {
                RuleAction.EnableGrayscale => "Включить grayscale",
                RuleAction.DisableGrayscale => "Выключить grayscale",
                _ => action.ToString()
            };
        }

        return value?.ToString() ?? "";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string str)
        {
            return str switch
            {
                "Включить grayscale" => RuleAction.EnableGrayscale,
                "Выключить grayscale" => RuleAction.DisableGrayscale,
                _ => RuleAction.DisableGrayscale
            };
        }

        return RuleAction.DisableGrayscale;
    }
}

/// <summary>
/// Конвертер для получения описания enum MatchType
/// </summary>
public class MatchTypeToDescriptionConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is MatchType matchType)
        {
            return matchType switch
            {
                MatchType.Exact => "Точное",
                MatchType.Contains => "Содержит",
                MatchType.Regex => "Regex",
                _ => matchType.ToString()
            };
        }

        return value?.ToString() ?? "";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string str)
        {
            return str switch
            {
                "Точное" => MatchType.Exact,
                "Содержит" => MatchType.Contains,
                "Regex" => MatchType.Regex,
                _ => MatchType.Contains
            };
        }

        return MatchType.Contains;
    }
}

/// <summary>
/// Конвертер для получения иконки действия
/// </summary>
public class RuleActionToIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is RuleAction action)
        {
            return action switch
            {
                RuleAction.EnableGrayscale => "⚫",
                RuleAction.DisableGrayscale => "⚪",
                _ => "⚪"
            };
        }

        return "⚪";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Конвертер для получения цвета действия
/// </summary>
public class RuleActionToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is RuleAction action)
        {
            return action switch
            {
                RuleAction.EnableGrayscale => new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(107, 114, 128)), // Серый для grayscale
                RuleAction.DisableGrayscale => new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(16, 185, 129)), // Зелёный для нормального
                _ => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Gray)
            };
        }

        return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Gray);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
