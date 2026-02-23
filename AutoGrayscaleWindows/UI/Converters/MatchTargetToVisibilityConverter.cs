using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using AutoGrayscaleWindows.Models;

namespace AutoGrayscaleWindows.Models
{
    /// <summary>
    /// Конвертер для преобразования MatchTarget в Visibility
    /// Показывает кнопку выбора файла только для режима Executable
    /// </summary>
    public class MatchTargetToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is MatchTarget matchTarget)
            {
                return matchTarget == MatchTarget.Executable ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
