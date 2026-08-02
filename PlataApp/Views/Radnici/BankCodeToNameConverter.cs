using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using Microsoft.EntityFrameworkCore;
using PlataData;

namespace PlataApp.Views.Radnici;

public class BankCodeToNameConverter : IValueConverter
{
    private static readonly ConcurrentDictionary<string, string> _bankNameCache = new();

    public static void ClearCache()
    {
        _bankNameCache.Clear();
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string code || string.IsNullOrWhiteSpace(code))
            return "Gotovina (Nije definisano)";

        code = code.Trim();
        int godina = AppConfig.ActiveGodina ?? DateTime.Now.Year;
        int mesec = AppConfig.ActiveMesec ?? DateTime.Now.Month;
        string key = $"{godina}_{mesec}_{code}";

        if (_bankNameCache.TryGetValue(key, out var cachedName))
        {
            return cachedName;
        }

        try
        {
            using var db = PlataDbContext.Create(AppConfig.DbPath);
            var b = db.Banke.AsNoTracking().FirstOrDefault(x => x.Godina == godina && x.Mesec == mesec && x.Sifra == code);
            if (b != null)
            {
                _bankNameCache[key] = b.Naziv;
                return b.Naziv;
            }
        }
        catch {}

        // Fallback
        string fallback = code;
        if (code == "1") fallback = "Gotovina";
        else if (code == "2") fallback = "BANKA INTESA";

        _bankNameCache[key] = fallback;
        return fallback;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
