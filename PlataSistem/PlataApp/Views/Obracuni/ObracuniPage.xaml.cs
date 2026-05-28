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
                            o.Godina, 
                            o.Mesec, 
                            COUNT(*) as BrojRadnika, 
                            SUM(o.NetoIsplata) as UkupnoNeto, 
                            SUM(o.BrutoZarada + o.BrutoBolovanje) as UkupnoBruto, 
                            MAX(o.DatumObracuna) as PoslednjiDatum,
                            COALESCE(MAX(p.VrBoda), 1860.34) as VrBoda
                        FROM ObracuniPlata o
                        LEFT JOIN Porezi p ON o.Godina = p.Godina AND o.Mesec = p.Mesec
                        GROUP BY o.Godina, o.Mesec";
                    
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
                                PoslednjiDatum = reader.IsDBNull(5) ? DateTime.MinValue : reader.GetDateTime(5),
                                VrednostBoda = reader.IsDBNull(6) ? 1860.34m : reader.GetDecimal(6)
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

            // Automatski selektuj aktivni period i skroluj do njega
            var active = summaries.FirstOrDefault(s => s.IsActive);
            if (active != null)
            {
                PeriodiGrid.SelectedItem = active;
                PeriodiGrid.ScrollIntoView(active);
            }

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

    private async void BtnObrisi_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is ObracunPeriodSummary selected)
        {
            var rez = MessageBox.Show(
                $"Da li ste sigurni da želite da obrišete kompletan obračun za period {selected.PeriodStr}?\n\n" +
                "Ova akcija će obrisati sve obračune plata i sačuvane radne sate za ovaj mesec, i vratiti rate kredita za zaposlene. Akcija je nepovratna!",
                "Potvrda brisanja obračuna",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (rez == MessageBoxResult.No) return;

            try
            {
                StatusMessage.Text = "Brisanje obračuna u toku...";

                // Učitaj obračune za taj period
                var obracuni = await _db.ObracuniPlata
                    .Where(o => o.Godina == selected.Godina && o.Mesec == selected.Mesec)
                    .ToListAsync();

                var targetDate = new DateTime(selected.Godina, selected.Mesec, 1);

                // Vrati rate kredita
                foreach (var o in obracuni)
                {
                    var radnikKrediti = await _db.Krediti
                        .Where(k => k.RadnikId == o.RadnikId)
                        .ToListAsync();

                    foreach (var k in radnikKrediti)
                    {
                        if (k.DatumPocetka <= targetDate && targetDate <= k.DatumPocetka.AddMonths(k.PlateneRate - 1))
                        {
                            k.PlateneRate--;
                            k.OstatakDuga = Math.Max(0, k.UkupanIznos - (k.PlateneRate * k.MesecnaRata));
                            k.Aktivan = true;
                            _db.Entry(k).State = EntityState.Modified;
                        }
                    }
                }

                // Obriši obračune
                _db.ObracuniPlata.RemoveRange(obracuni);

                // Obriši radne sate
                var sati = await _db.RadniSati
                    .Where(s => s.Godina == selected.Godina && s.Mesec == selected.Mesec)
                    .ToListAsync();
                _db.RadniSati.RemoveRange(sati);

                await _db.SaveChangesAsync();

                MessageBox.Show($"Uspešno obrisan obračun za period {selected.PeriodStr}.", "Uspeh", MessageBoxButton.OK, MessageBoxImage.Information);

                // Ako je obrisani period bio aktivni period u AppConfig, resetuj aktivni period na najnoviji preostali ili na podrazumevani
                if (AppConfig.ActiveGodina == selected.Godina && AppConfig.ActiveMesec == selected.Mesec)
                {
                    var najnovijiPreostali = await _db.ObracuniPlata
                        .OrderByDescending(o => o.Godina)
                        .ThenByDescending(o => o.Mesec)
                        .FirstOrDefaultAsync();

                    if (najnovijiPreostali != null)
                    {
                        AppConfig.ActiveGodina = najnovijiPreostali.Godina;
                        AppConfig.ActiveMesec = najnovijiPreostali.Mesec;
                    }
                    else
                    {
                        AppConfig.ActiveGodina = DateTime.Now.Year;
                        AppConfig.ActiveMesec = DateTime.Now.Month;
                    }
                }

                UcitajPeriodiSummary();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Greška prilikom brisanja obračuna: {ex.Message}", "Greška", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusMessage.Text = "Greška pri brisanju.";
            }
        }
    }

    private void BtnNoviObracun_Click(object sender, RoutedEventArgs e)
    {
        var selected = PeriodiGrid.SelectedItem as ObracunPeriodSummary;

        var window = new Obracun.NoviObracunWindow(selected)
        {
            Owner = Window.GetWindow(this)
        };
        if (window.ShowDialog() == true)
        {
            UcitajPeriodiSummary();
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
    public decimal VrednostBoda { get; set; }
    public decimal UkupnoNeto { get; set; }
    public decimal UkupnoBruto { get; set; }
    public DateTime PoslednjiDatum { get; set; }

    public bool IsActive => AppConfig.ActiveGodina == Godina && AppConfig.ActiveMesec == Mesec;
}
