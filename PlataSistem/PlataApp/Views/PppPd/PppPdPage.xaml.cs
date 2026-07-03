using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Microsoft.Win32;
using PlataData.Models;
using PlataApp.Services;

namespace PlataApp.Views.PppPd;

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
                vm.SelectedTipIsplatioca
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
                    vm.SelectedTipIsplatioca
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
        if (value is ObracunPlate o && o.Radnik != null)
        {
            string svp = "101101000";
            bool jePenzioner = !string.IsNullOrWhiteSpace(o.Radnik.Radno_Mesto) && 
                               o.Radnik.Radno_Mesto.TrimStart().StartsWith("109");

            if (o.BrutoBolovanje > o.BrutoZarada)
            {
                svp = "109101000";
            }
            else if (!string.IsNullOrWhiteSpace(o.Radnik.Radno_Mesto) && 
                     o.Radnik.Radno_Mesto.Length == 9 && 
                     o.Radnik.Radno_Mesto.All(char.IsDigit))
            {
                svp = o.Radnik.Radno_Mesto;
            }
            else if (jePenzioner)
            {
                svp = "101109000";
            }
            return svp;
        }
        return "101101000";
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
