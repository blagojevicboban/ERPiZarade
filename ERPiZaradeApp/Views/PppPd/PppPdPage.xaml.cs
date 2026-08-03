using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Microsoft.Win32;
using ERPiZaradeData.Models;
using ERPiZaradeApp.Services;

namespace ERPiZaradeApp.Views.PppPd;

public partial class PppPdPage : Page
{
    public PppPdPage()
    {
        InitializeComponent();
    }

    private void BtnKopirajXml_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not PppPdViewModel vm) return;
        
        if (vm.Obracuni == null || vm.Obracuni.Count == 0)
        {
            MessageBox.Show("Nema obračuna za generisanje XML-a u odabranom periodu.", "Upozorenje", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var xmlService = new XmlExportService();
            string xml = xmlService.GeneratePppPdXml(
                vm.Obracuni.ToList(),
                vm.DatumPlacanja,
                vm.Pib,
                vm.MaticniBroj,
                vm.Naziv,
                vm.Sediste,
                vm.Telefon,
                vm.Adresa,
                vm.Email,
                vm.KlijentskaOznaka,
                vm.SelectedVrstaPrijave,
                vm.SelectedOznakaZaKonacnu,
                vm.SelectedNajnizaOsnovica,
                vm.SelectedTipIsplatioca,
                vm.BrojKalendarskihDana,
                mfpPoOlaksici: vm.MfpPoOlaksici,
                izmena: vm.Izmena
            );

            Clipboard.SetText(xml);
            MessageBox.Show("PPP-PD XML je uspešno kopiran u privremenu memoriju (Clipboard).", "Uspeh", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Greška pri generisanju XML-a: {ex.Message}", "Greška", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnGenerisiXml_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not PppPdViewModel vm) return;

        if (vm.Obracuni == null || vm.Obracuni.Count == 0)
        {
            MessageBox.Show("Nema obračuna za generisanje XML-a u odabranom periodu.", "Upozorenje", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var sfd = new SaveFileDialog
        {
            Filter = "XML dokument (*.xml)|*.xml",
            FileName = $"PPP-PD_{vm.SelectedMesec:D2}_{vm.SelectedGodina}.xml",
            Title = "Sačuvaj PPP-PD XML poresku deklaraciju"
        };

        if (sfd.ShowDialog() == true)
        {
            try
            {
                var xmlService = new XmlExportService();
                string xml = xmlService.GeneratePppPdXml(
                    vm.Obracuni.ToList(),
                    vm.DatumPlacanja,
                    vm.Pib,
                    vm.MaticniBroj,
                    vm.Naziv,
                    vm.Sediste,
                    vm.Telefon,
                    vm.Adresa,
                    vm.Email,
                    vm.KlijentskaOznaka,
                    vm.SelectedVrstaPrijave,
                    vm.SelectedOznakaZaKonacnu,
                    vm.SelectedNajnizaOsnovica,
                    vm.SelectedTipIsplatioca,
                    vm.BrojKalendarskihDana,
                    mfpPoOlaksici: vm.MfpPoOlaksici,
                    izmena: vm.Izmena
                );

                System.IO.File.WriteAllText(sfd.FileName, xml, System.Text.Encoding.UTF8);

                MessageBox.Show("PPP-PD XML poreska deklaracija je uspešno generisana i sačuvana.", 
                    "Uspeh", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Greška pri generisanju XML-a: {ex.Message}", "Greška", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}

// ── VREDNOSNI KONVERTERI ZA PREGLED ───────────────────────────────────

/// <summary>
/// Naziv vrste izmene za padajuću listu. Brojevi su iz specifikacije Poreske uprave
/// (pozicija 1.5) i stoje uz naziv, da se pri kontroli vidi šta je tačno poslato.
/// </summary>
public class VrstaIzmeneConverter : IValueConverter
{
    public static readonly VrstaIzmeneConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is VrstaIzmenePrijave v
            ? v switch
            {
                VrstaIzmenePrijave.Izmena => "1 - Izmena",
                VrstaIzmenePrijave.PoNalazuKontrole => "2 - Po nalazu kontrole",
                VrstaIzmenePrijave.PoNaloguSuda => "3 - Po nalogu suda",
                _ => "— Redovna prijava (bez izmene)"
            }
            : "";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>Naziv osnova podnošenja izmenjene prijave (pozicija 1.6a).</summary>
public class OsnovIzmeneConverter : IValueConverter
{
    public static readonly OsnovIzmeneConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is OsnovIzmenePrijave o
            ? o switch
            {
                OsnovIzmenePrijave.ZalbaPrviStepen => "1 - Žalba, prvi stepen",
                OsnovIzmenePrijave.ZalbaDrugiStepen => "2 - Žalba, drugi stepen",
                OsnovIzmenePrijave.PoNaloguSuda => "3 - Po nalogu suda",
                _ => "— Bez osnova"
            }
            : "";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class BrutoConverter : IValueConverter
{
    public static readonly BrutoConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ObracunPlate o)
        {
            return o.BrutoZarada + o.BrutoBolovanje;
        }
        return 0m;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class PioTotalConverter : IValueConverter
{
    public static readonly PioTotalConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ObracunPlate o)
        {
            return o.DoprinosPioRadnik + o.DoprinosPioPoslodavac;
        }
        return 0m;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class ZdrTotalConverter : IValueConverter
{
    public static readonly ZdrTotalConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ObracunPlate o)
        {
            return o.DoprinosZdravstvoRadnik + o.DoprinosZdravstvoPoslodavac;
        }
        return 0m;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class NezTotalConverter : IValueConverter
{
    public static readonly NezTotalConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ObracunPlate o)
        {
            return o.DoprinosNezaposlenostRadnik + o.DoprinosNezaposlenostPoslodavac;
        }
        return 0m;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class SvpConverter : IValueConverter
{
    public static readonly SvpConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // Prikaz mora da koristi istu logiku kao izvoz — inače ekran pokazuje jednu šifru,
        // a u prijavu ode druga.
        if (value is ObracunPlate o && o.Radnik != null)
            return SvpService.Odredi(o);

        return SvpService.RedovnaZarada;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class OsnovicaDoprinosaConverter : IValueConverter
{
    public static readonly OsnovicaDoprinosaConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ObracunPlate o)
        {
            decimal bruto = o.BrutoZarada + o.BrutoBolovanje;
            decimal pioRadnik = o.DoprinosPioRadnik;
            decimal pioPoslodavac = o.DoprinosPioPoslodavac;
            decimal totalPio = pioRadnik + pioPoslodavac;

            decimal osnovicaDoprinosa = bruto;
            if (totalPio > 0 && bruto > 0)
            {
                osnovicaDoprinosa = Math.Round(totalPio / 0.24m, 2);
            }
            return osnovicaDoprinosa;
        }
        return 0m;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class EfektivniSatiConverter : IValueConverter
{
    public static readonly EfektivniSatiConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ObracunPlate o)
        {
            return o.UkupnoSati;
        }
        return 0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class FondSatiConverter : IValueConverter
{
    public static readonly FondSatiConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ObracunPlate o)
        {
            return o.UkupnoSati;
        }
        return 0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
