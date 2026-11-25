using System;
using System.Globalization;
using System.Windows.Data;

namespace SandFactory.UI;

public class RoadProgressToXConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double progress)
        {
            // Превращаем прогресс (-50...150) в координату X (0...500)
            return progress * 3.5;
        }
        return 0.0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}