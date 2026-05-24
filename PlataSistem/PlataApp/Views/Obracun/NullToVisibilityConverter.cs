using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace PlataApp.Views.Obracun;

public class NullToVisibilityConverter : IValueConverter
{
    public static readonly NullToVisibilityConverter Instance = new();
    public static readonly NullToVisibilityConverter InverseInstance = new() { Inverse = true };

    public bool Inverse { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool hasValue = value != null;
        if (Inverse)
            hasValue = !hasValue;

        return hasValue ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
