using System;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using PlataData;

namespace PlataApp.Views.Radnici;

public class BankCodeToNameConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string code || string.IsNullOrWhiteSpace(code))
            return "Gotovina (Nije definisano)";

        code = code.Trim();

        try
        {
            using var db = PlataDbContext.Create(AppConfig.DbPath);
            int godina = AppConfig.ActiveGodina ?? DateTime.Now.Year;
            int mesec = AppConfig.ActiveMesec ?? DateTime.Now.Month;
            
            var b = db.Banke.FirstOrDefault(x => x.Godina == godina && x.Mesec == mesec && x.Sifra == code);
            if (b != null)
            {
                return $"{b.Naziv}";
            }
        }
        catch {}

        // Fallback
        if (code == "1") return "Gotovina";
        if (code == "2") return "BANKA INTESA";
        return code;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
