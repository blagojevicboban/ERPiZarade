using System;
using System.Globalization;
using System.Windows.Data;

namespace PlataApp.Views.Radnici;

public class NullToBoolConverter : IValueConverter
{
    public static readonly NullToBoolConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null || value == System.Windows.DependencyProperty.UnsetValue)
            return false;
        return true;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
