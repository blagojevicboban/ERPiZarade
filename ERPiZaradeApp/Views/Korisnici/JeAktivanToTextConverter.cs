using System;
using System.Globalization;
using System.Windows.Data;

namespace ERPiZaradeApp.Views.Korisnici;

public class JeAktivanToTextConverter : IValueConverter
{
    public static readonly JeAktivanToTextConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? "Aktivan" : "Neaktivan";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
