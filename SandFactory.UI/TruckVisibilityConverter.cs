using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SandFactory.UI;

public class TruckVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double progress)
        {
            // Если параметр "ScaleX", возвращаем 1 (вправо) или -1 (влево для разворота)
            if (parameter?.ToString() == "ScaleX")
            {
                return progress < 0 ? -1.0 : 1.0;
            }

            // Если параметр "Visibility", скрываем грузовик, если он за пределами экрана
            if (parameter?.ToString() == "Visibility")
            {
                // Если прогресс от 115 (уехал вправо) и до -15 (уехал влево), скрываем
                if (progress > 115 || progress < -15)
                    return Visibility.Collapsed;
                return Visibility.Visible;
            }
        }
        return Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}