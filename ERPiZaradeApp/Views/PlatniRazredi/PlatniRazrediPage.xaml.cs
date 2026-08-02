using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ERPiZaradeData;
using ERPiZaradeData.Models;

namespace ERPiZaradeApp.Views.PlatniRazredi;

public partial class PlatniRazrediPage : Page
{
    private PlataDbContext _db;
    private PlatniRazred? _currentRazredi;

    public PlatniRazrediPage()
    {
        InitializeComponent();
        _db = PlataDbContext.Create(AppConfig.DbPath);

        UcitajPlatneRazrede();
    }

    private void UcitajPlatneRazrede()
    {
        try
        {
            _currentRazredi = _db.PlatniRazredi.FirstOrDefault();
            if (_currentRazredi == null)
            {
                _currentRazredi = new PlatniRazred
                {
                    R1 = 51297.00m, R2 = 51297.00m, R3 = 51297.00m, R4 = 51297.00m, R5 = 51297.00m, R6 = 51297.00m, R7 = 51297.00m, R8 = 51297.00m, R9 = 0m,
                    P1 = 51297.00m, P2 = 51297.00m, P3 = 51297.00m, P4 = 51297.00m, P5 = 51297.00m, P6 = 51297.00m, P7 = 51297.00m, P8 = 51297.00m, P9 = 0m
                };
            }
            PopuniRazrediFormu();
        }
        catch (Exception ex)
        {
            StatusMessage.Text = $"Greška pri učitavanju platnih razreda: {ex.Message}";
        }
    }

    private void PopuniRazrediFormu()
    {
        if (_currentRazredi == null) return;

        TxtR1.Text = _currentRazredi.R1.ToString("N2");
        TxtR2.Text = _currentRazredi.R2.ToString("N2");
        TxtR3.Text = _currentRazredi.R3.ToString("N2");
        TxtR4.Text = _currentRazredi.R4.ToString("N2");
        TxtR5.Text = _currentRazredi.R5.ToString("N2");
        TxtR6.Text = _currentRazredi.R6.ToString("N2");
        TxtR7.Text = _currentRazredi.R7.ToString("N2");
        TxtR8.Text = _currentRazredi.R8.ToString("N2");
        TxtR9.Text = _currentRazredi.R9.ToString("N2");

        TxtP1.Text = _currentRazredi.P1.ToString("N2");
        TxtP2.Text = _currentRazredi.P2.ToString("N2");
        TxtP3.Text = _currentRazredi.P3.ToString("N2");
        TxtP4.Text = _currentRazredi.P4.ToString("N2");
        TxtP5.Text = _currentRazredi.P5.ToString("N2");
        TxtP6.Text = _currentRazredi.P6.ToString("N2");
        TxtP7.Text = _currentRazredi.P7.ToString("N2");
        TxtP8.Text = _currentRazredi.P8.ToString("N2");
        TxtP9.Text = _currentRazredi.P9.ToString("N2");
    }

    private void BtnSacuvajRazredi_Click(object sender, RoutedEventArgs e)
    {
        if (_currentRazredi == null) return;

        try
        {
            _currentRazredi.R1 = ParseDecimal(TxtR1.Text);
            _currentRazredi.R2 = ParseDecimal(TxtR2.Text);
            _currentRazredi.R3 = ParseDecimal(TxtR3.Text);
            _currentRazredi.R4 = ParseDecimal(TxtR4.Text);
            _currentRazredi.R5 = ParseDecimal(TxtR5.Text);
            _currentRazredi.R6 = ParseDecimal(TxtR6.Text);
            _currentRazredi.R7 = ParseDecimal(TxtR7.Text);
            _currentRazredi.R8 = ParseDecimal(TxtR8.Text);
            _currentRazredi.R9 = ParseDecimal(TxtR9.Text);

            _currentRazredi.P1 = ParseDecimal(TxtP1.Text);
            _currentRazredi.P2 = ParseDecimal(TxtP2.Text);
            _currentRazredi.P3 = ParseDecimal(TxtP3.Text);
            _currentRazredi.P4 = ParseDecimal(TxtP4.Text);
            _currentRazredi.P5 = ParseDecimal(TxtP5.Text);
            _currentRazredi.P6 = ParseDecimal(TxtP6.Text);
            _currentRazredi.P7 = ParseDecimal(TxtP7.Text);
            _currentRazredi.P8 = ParseDecimal(TxtP8.Text);
            _currentRazredi.P9 = ParseDecimal(TxtP9.Text);

            if (_currentRazredi.Id == 0)
            {
                _db.PlatniRazredi.Add(_currentRazredi);
            }
            else
            {
                _db.Entry(_currentRazredi).State = Microsoft.EntityFrameworkCore.EntityState.Modified;
            }

            _db.SaveChanges();

            StatusMessage.Text = "Platni razredi su uspešno sačuvani!";
            MessageBox.Show("Platni razredi su uspešno sačuvani!", "Uspeh", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Greška pri čuvanju platnih razreda: {ex.Message}", "Greška", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private decimal ParseDecimal(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        text = text.Replace(".", "").Replace(",", ".").Trim();
        if (decimal.TryParse(text, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal val))
        {
            return val;
        }
        return 0;
    }
}
