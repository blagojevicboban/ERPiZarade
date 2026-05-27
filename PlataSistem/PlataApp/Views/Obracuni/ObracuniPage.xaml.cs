using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PlataData;
using PlataData.Models;

namespace PlataApp.Views.Obracuni;

public partial class ObracuniPage : Page
{
    private PlataDbContext _db;

    public ObracuniPage()
    {
        InitializeComponent();
        _db = PlataDbContext.Create(AppConfig.DbPath);

        UcitajNazivFirme();
        UcitajPeriodiSummary();
    }

    private void UcitajNazivFirme()
    {
        try
        {
            var firma = _db.Firme.FirstOrDefault();
            if (firma != null && !string.IsNullOrWhiteSpace(firma.Naziv))
            {
                FirmaSubtitle.Text = $"Pregled svih obračunatih perioda i finansijskih rekapitulacija za firmu: {firma.Naziv}";
            }
        }
        catch { }
    }

    private void UcitajPeriodiSummary()
    {
        try
        {
            var summaries = _db.ObracuniPlata
                .GroupBy(o => new { o.Godina, o.Mesec })
                .Select(g => new ObracunPeriodSummary
                {
                    Godina = g.Key.Godina,
                    Mesec = g.Key.Mesec,
                    BrojRadnika = g.Count(),
                    UkupnoNeto = g.Sum(o => o.NetoIsplata),
                    UkupnoBruto = g.Sum(o => o.BrutoZarada + o.BrutoBolovanje),
                    PoslednjiDatum = g.Max(o => o.DatumObracuna)
                })
                .ToList();

            // Poredak od najnovijeg ka najstarijem
            summaries = summaries
                .OrderByDescending(s => s.Godina)
                .ThenByDescending(s => s.Mesec)
                .ToList();

            PeriodiGrid.ItemsSource = summaries;
            StatusMessage.Text = $"Pronađeno je ukupno {summaries.Count} obračunatih perioda u sistemu.";
        }
        catch (Exception ex)
        {
            StatusMessage.Text = $"Greška pri učitavanju istorijskih obračuna: {ex.Message}";
        }
    }

    private void PeriodiGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (PeriodiGrid.SelectedItem is ObracunPeriodSummary selected)
        {
            OtvorPeriod(selected);
        }
    }

    private void BtnOtvori_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is ObracunPeriodSummary selected)
        {
            OtvorPeriod(selected);
        }
    }

    private void OtvorPeriod(ObracunPeriodSummary summary)
    {
        var mainWindow = Application.Current.MainWindow as MainWindow;
        if (mainWindow != null)
        {
            mainWindow.NavigateToObracun(summary.Godina, summary.Mesec);
        }
    }
}

public class ObracunPeriodSummary
{
    public int Godina { get; set; }
    public int Mesec { get; set; }
    
    public string PeriodStr
    {
        get
        {
            string[] meseciStr = {
                "Januar", "Februar", "Mart", "April", "Maj", "Jun",
                "Jul", "Avgust", "Septembar", "Oktobar", "Novembar", "Decembar"
            };
            if (Mesec >= 1 && Mesec <= 12)
            {
                return $"{meseciStr[Mesec - 1]} {Godina}";
            }
            return $"{Mesec:D2} / {Godina}";
        }
    }

    public int BrojRadnika { get; set; }
    public decimal UkupnoNeto { get; set; }
    public decimal UkupnoBruto { get; set; }
    public DateTime PoslednjiDatum { get; set; }
}
