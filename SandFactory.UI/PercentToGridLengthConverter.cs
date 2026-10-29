using System;
using System.Globalization;
using System.Windows.Data;

namespace SandFactory.UI;

public class PercentToGridLengthConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double percent)
        {
            // Ограничиваем от 0 до 100, чтобы полоска не улетала за края
            if (percent < 0) percent = 0;
            if (percent > 100) percent = 100;

            // Возвращаем ширину в звездах (GridLength). 50% = 50*, остальное - *
            return new System.Windows.GridLength(percent, System.Windows.GridUnitType.Star);
        }
        return new System.Windows.GridLength(0, System.Windows.GridUnitType.Star);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}