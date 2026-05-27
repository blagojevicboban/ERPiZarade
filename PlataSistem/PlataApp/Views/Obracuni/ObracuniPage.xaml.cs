using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.EntityFrameworkCore;
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
            var summaries = new List<ObracunPeriodSummary>();
            using (var conn = _db.Database.GetDbConnection())
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        SELECT 
                            Godina, 
                            Mesec, 
                            COUNT(*) as BrojRadnika, 
                            SUM(NetoIsplata) as UkupnoNeto, 
                            SUM(BrutoZarada + BrutoBolovanje) as UkupnoBruto, 
                            MAX(DatumObracuna) as PoslednjiDatum
                        FROM ObracuniPlata
                        GROUP BY Godina, Mesec";
                    
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            summaries.Add(new ObracunPeriodSummary
                            {
                                Godina = reader.GetInt32(0),
                                Mesec = reader.GetInt32(1),
                                BrojRadnika = reader.GetInt32(2),
                                UkupnoNeto = reader.IsDBNull(3) ? 0 : reader.GetDecimal(3),
                                UkupnoBruto = reader.IsDBNull(4) ? 0 : reader.GetDecimal(4),
                                PoslednjiDatum = reader.IsDBNull(5) ? DateTime.MinValue : reader.GetDateTime(5)
                            });
                        }
                    }
                }
            }

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
        AppConfig.ActiveGodina = summary.Godina;
        AppConfig.ActiveMesec = summary.Mesec;

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
