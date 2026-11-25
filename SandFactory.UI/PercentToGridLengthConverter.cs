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
            if (percent < 0) percent = 0;
            if (percent > 100) percent = 100;
            return new System.Windows.GridLength(percent, System.Windows.GridUnitType.Star);
        }
        return new System.Windows.GridLength(0, System.Windows.GridUnitType.Star);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}