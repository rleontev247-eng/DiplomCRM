using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace MyFirstCRM
{
    // Конвертер для преобразования статуса сделки в цвет
    public class StatusToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is DealStatus status)
            {
                return status switch
                {
                    DealStatus.New => new SolidColorBrush(Color.FromArgb(255, 59, 130, 246)),       // Синий
                    DealStatus.InProgress => new SolidColorBrush(Color.FromArgb(255, 245, 158, 11)), // Оранжевый
                    DealStatus.Successful => new SolidColorBrush(Color.FromArgb(255, 16, 185, 129)), // Зеленый
                    DealStatus.Failed => new SolidColorBrush(Color.FromArgb(255, 239, 68, 68)),      // Красный
                    _ => new SolidColorBrush(Colors.Gray)
                };
            }
            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    // Конвертер для ProgressBar (воронка продаж)
    public class ProgressBarClipConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length == 3 && values[0] is double width && values[1] is double value && values[2] is double maximum)
            {
                if (maximum == 0) return 0;
                double progress = (value / maximum) * width;
                return progress;
            }
            return 0;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    // Конвертер для преобразования приоритета в цвет
    public class PriorityToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Priority priority)
            {
                return priority switch
                {
                    Priority.Low => new SolidColorBrush(Color.FromArgb(255, 107, 114, 128)),     // Серый
                    Priority.Medium => new SolidColorBrush(Color.FromArgb(255, 59, 130, 246)),   // Синий
                    Priority.High => new SolidColorBrush(Color.FromArgb(255, 245, 158, 11)),     // Оранжевый
                    Priority.Critical => new SolidColorBrush(Color.FromArgb(255, 239, 68, 68)),  // Красный
                    _ => new SolidColorBrush(Colors.Gray)
                };
            }
            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    // Конвертер для преобразования DateTime в строку
    public class DateTimeToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is DateTime dateTime)
            {
                return dateTime.ToString("dd.MM.yyyy HH:mm");
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    // Конвертер для преобразования статуса синхронизации в цвет
    public class SyncStatusToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is SyncStatus status)
            {
                return status switch
                {
                    SyncStatus.Success => new SolidColorBrush(Color.FromArgb(255, 16, 185, 129)),   // Зеленый
                    SyncStatus.Failed => new SolidColorBrush(Color.FromArgb(255, 239, 68, 68)),      // Красный
                    SyncStatus.InProgress => new SolidColorBrush(Color.FromArgb(255, 59, 130, 246)), // Синий
                    SyncStatus.Partial => new SolidColorBrush(Color.FromArgb(255, 245, 158, 11)),   // Оранжевый
                    SyncStatus.Conflict => new SolidColorBrush(Color.FromArgb(255, 168, 85, 247)),  // Фиолетовый
                    _ => new SolidColorBrush(Colors.Gray)
                };
            }
            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}