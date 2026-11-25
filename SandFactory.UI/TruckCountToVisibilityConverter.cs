using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SandFactory.UI;

public class TruckCountToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // value - это ActiveTrucks (сколько куплено)
        // parameter - это индекс грузовика ("0", "1", "2", "3", "4")
        if (value is int activeTrucks && parameter != null)
        {
            if (int.TryParse(parameter.ToString(), out int truckIndex))
            {
                // Если индекс меньше количества купленных - показываем, иначе скрываем
                return truckIndex < activeTrucks ? Visibility.Visible : Visibility.Collapsed;
            }
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}