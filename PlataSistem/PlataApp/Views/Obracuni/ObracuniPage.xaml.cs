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
                            SUM(o.NetoIsplata + o.PorezNaDohodak + o.DoprinosPioRadnik + o.DoprinosZdravstvoRadnik + o.DoprinosNezaposlenostRadnik + o.KreditObustava + o.Samodoprinosi + o.DoprinosPioPoslodavac + o.DoprinosZdravstvoPoslodavac + o.DoprinosNezaposlenostPoslodavac) as UkupnoBruto2,
                            MAX(o.DatumObracuna) as PoslednjiDatum,
                            COALESCE(MAX(p.VrBoda), 1860.34) as VrBoda,
                            MAX(CAST(o.Zakljucan AS INTEGER)) as Zakljucan
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
                                UkupnoBruto2 = reader.IsDBNull(5) ? 0 : reader.GetDecimal(5),
                                PoslednjiDatum = reader.IsDBNull(6) ? DateTime.MinValue : reader.GetDateTime(6),
                                VrednostBoda = reader.IsDBNull(7) ? 1860.34m : reader.GetDecimal(7),
                                Zakljucan = reader.IsDBNull(8) ? false : (reader.GetInt32(8) == 1)
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
    private void BtnZakljucajSve_Click(object sender, RoutedEventArgs e)
    {
        var res = MessageBox.Show("Da li ste sigurni da želite da zaključate SVE otključane obračunske periode?\n\nNakon ovoga, izmena podataka u starim obračunima neće biti moguća.",
                                  "Potvrda", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (res == MessageBoxResult.Yes)
        {
            try
            {
                StatusMessage.Text = "Zaključavanje...";
                
                // Set Zakljucan = 1 for all rows that are currently 0 or NULL
                _db.Database.ExecuteSqlRaw("UPDATE ObracuniPlata SET Zakljucan = 1 WHERE Zakljucan = 0 OR Zakljucan IS NULL");
                
                StatusMessage.Text = "Svi obračunski periodi su uspešno zaključani.";
                MessageBox.Show("Svi obračunski periodi su uspešno zaključani.", "Uspeh", MessageBoxButton.OK, MessageBoxImage.Information);
                
                UcitajPeriodiSummary(); // Osveži tabelu
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Greška prilikom zaključavanja: {ex.Message}", "Greška", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusMessage.Text = "Greška prilikom zaključavanja.";
            }
        }
    }

    private void BtnZakljucaj_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is ObracunPeriodSummary row)
        {
            bool noviStatus = !row.Zakljucan;
            string akcija = noviStatus ? "zaključate" : "otključate";

            var res = MessageBox.Show($"Da li ste sigurni da želite da {akcija} obračunski period {row.PeriodStr}?",
                                      "Potvrda", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (res == MessageBoxResult.Yes)
            {
                try
                {
                    _db.Database.ExecuteSqlRaw("UPDATE ObracuniPlata SET Zakljucan = {0} WHERE Godina = {1} AND Mesec = {2}",
                        noviStatus ? 1 : 0, row.Godina, row.Mesec);
                    
                    StatusMessage.Text = $"Period {row.PeriodStr} je uspešno {(noviStatus ? "zaključan" : "otključan")}.";
                    UcitajPeriodiSummary(); // Osveži tabelu
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Greška prilikom ažuriranja statusa: {ex.Message}", "Greška", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }

    private async void BtnObrisi_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is ObracunPeriodSummary selected)
        {
            if (selected.Zakljucan)
            {
                MessageBox.Show("Ovaj period je zaključan i ne može se obrisati.", "Upozorenje", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

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

                // Obriši specifične parametre poreza za taj mesec
                var porezi = await _db.Porezi
                    .Where(p => p.Godina == selected.Godina && p.Mesec == selected.Mesec)
                    .ToListAsync();
                _db.Porezi.RemoveRange(porezi);

                // Obriši specifične stope doprinosa za taj mesec
                var doprinosi = await _db.Doprinosi
                    .Where(d => d.Godina == selected.Godina && d.Mesec == selected.Mesec)
                    .ToListAsync();
                _db.Doprinosi.RemoveRange(doprinosi);

                // Obriši specifični šifrarnik banaka za taj mesec
                var banke = await _db.Banke
                    .Where(b => b.Godina == selected.Godina && b.Mesec == selected.Mesec)
                    .ToListAsync();
                _db.Banke.RemoveRange(banke);

                // Obriši samodoprinose za taj mesec
                var samodoprinosi = await _db.Samodoprinosi
                    .Where(s => s.Godina == selected.Godina && s.Mesec == selected.Mesec)
                    .ToListAsync();
                _db.Samodoprinosi.RemoveRange(samodoprinosi);

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

    private async void BtnRetroaktivniDoprinosi_Click(object sender, RoutedEventArgs e)
    {
        var rez = MessageBox.Show(
            "Ova akcija će preračunati doprinose na teret POSLODAVCA za sve obračune gde su trenutno 0.\n\n" +
            "Stope se uzimaju iz tabele Doprinosi za odgovarajući mesec (ili najbliži prethodni).\n" +
            "Osnovica = BrutoZarada + BrutoBolovanje (kao u redovnom obračunu).\n\n" +
            "Da li želite da nastavite?",
            "Retroaktivni preračun doprinosa poslodavca",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (rez == MessageBoxResult.No) return;

        BtnRetroaktivniDoprinosi.IsEnabled = false;
        StatusMessage.Text = "Retroaktivni preračun u toku... Molimo sačekajte.";

        try
        {
            // Učitaj sve obračune gde su doprinosi poslodavca = 0 i koji nisu zaključani
            var obracuniZaAzuriranje = await _db.ObracuniPlata
                .Include(o => o.Radnik)
                .Where(o => o.DoprinosPioPoslodavac == 0
                         && o.DoprinosZdravstvoPoslodavac == 0
                         && o.DoprinosNezaposlenostPoslodavac == 0
                         && !o.Zakljucan)
                .ToListAsync();

            if (obracuniZaAzuriranje.Count == 0)
            {
                MessageBox.Show("Svi obračuni već imaju preračunate doprinose poslodavca. Nema šta da se ažurira.",
                    "Informacija", MessageBoxButton.OK, MessageBoxImage.Information);
                StatusMessage.Text = "Nema obračuna za ažuriranje.";
                BtnRetroaktivniDoprinosi.IsEnabled = true;
                return;
            }

            // Učitaj sve periode doprinosa iz baze (keš za performanse)
            var sviDoprinosi = await _db.Doprinosi.ToListAsync();

            // Učitaj sve platne razrede
            var platniRazredi = await _db.PlatniRazredi.FirstOrDefaultAsync();
            decimal defaultMinBase = 51297.00m;

            int azurirano = 0;
            foreach (var o in obracuniZaAzuriranje)
            {
                // Pronađi stope doprinosa za ovaj period
                var doprinosiZaPeriod = sviDoprinosi
                    .Where(d => d.Godina == o.Godina && d.Mesec == o.Mesec)
                    .ToList();

                if (!doprinosiZaPeriod.Any())
                {
                    // Nađi najbliži prethodni period
                    var closest = sviDoprinosi
                        .Where(d => d.Godina < o.Godina || (d.Godina == o.Godina && d.Mesec < o.Mesec))
                        .OrderByDescending(d => d.Godina)
                        .ThenByDescending(d => d.Mesec)
                        .FirstOrDefault();

                    if (closest != null)
                    {
                        doprinosiZaPeriod = sviDoprinosi
                            .Where(d => d.Godina == closest.Godina && d.Mesec == closest.Mesec)
                            .ToList();
                    }
                }

                // Standard rates variables initialized to defaults
                decimal bossPio = 0.1000m;
                decimal bossZdr = 0.0515m;
                decimal bossNez = 0.0000m;

                // Dinamička inicijalizacija stopa za poslodavca na osnovu perioda ukoliko nema vrednosti u bazi
                if (o.Godina >= 2023)
                {
                    bossPio = 0.1000m;
                    bossNez = 0.0000m;
                }
                else if (o.Godina == 2022)
                {
                    bossPio = 0.1100m;
                    bossNez = 0.0000m;
                }
                else if (o.Godina >= 2020 || (o.Godina == 2019 && o.Mesec == 12))
                {
                    bossPio = 0.1150m;
                    bossNez = 0.0000m;
                }
                else
                {
                    bossPio = 0.1200m;
                    bossNez = 0.0075m;
                }

                // Overlay sa bazom podataka
                if (doprinosiZaPeriod.Any())
                {
                    var pioRec = doprinosiZaPeriod.FirstOrDefault(d => d.RedniBroj == 1);
                    if (pioRec != null && pioRec.ProcPosl > 0)
                    {
                        bossPio = pioRec.ProcPosl / 100m;
                    }

                    var zdrRec = doprinosiZaPeriod.FirstOrDefault(d => d.RedniBroj == 2);
                    if (zdrRec != null && zdrRec.ProcPosl > 0)
                    {
                        bossZdr = zdrRec.ProcPosl / 100m;
                    }

                    var nezRec = doprinosiZaPeriod.FirstOrDefault(d => d.RedniBroj == 3);
                    if (nezRec != null && nezRec.ProcPosl > 0)
                    {
                        bossNez = nezRec.ProcPosl / 100m;
                    }
                }

                // Penzioneri (radno mesto počinje sa "109") — nema doprinosa za nezaposlenost
                bool jePenzioner = !string.IsNullOrWhiteSpace(o.Radnik?.Radno_Mesto)
                                   && o.Radnik.Radno_Mesto.TrimStart().StartsWith("109");
                if (jePenzioner)
                {
                    bossNez = 0m;
                }

                decimal totalBruto = o.BrutoZarada + o.BrutoBolovanje;

                // Određivanje minimalne osnovice i maksimalne osnovice iz šifrarnika
                decimal minBase = defaultMinBase;
                decimal maxBase = 0m;

                if (doprinosiZaPeriod.Any())
                {
                    var pioRec = doprinosiZaPeriod.FirstOrDefault(d => d.RedniBroj == 1);
                    if (pioRec != null)
                    {
                        if (pioRec.NajnizaOsnovica > 0) minBase = pioRec.NajnizaOsnovica;
                        if (pioRec.NajvisaOsnovica > 0) maxBase = pioRec.NajvisaOsnovica;
                    }
                }

                if (platniRazredi != null && o.Radnik != null && !string.IsNullOrEmpty(o.Radnik.Kategorija))
                {
                    int.TryParse(o.Radnik.Kategorija, out int katId);
                    minBase = katId switch
                    {
                        1 => platniRazredi.R1,
                        2 => platniRazredi.R2,
                        3 => platniRazredi.R3,
                        4 => platniRazredi.R4,
                        5 => platniRazredi.R5,
                        6 => platniRazredi.R6,
                        7 => platniRazredi.R7,
                        8 => platniRazredi.R8,
                        _ => minBase
                    };
                }
                else if (o.Radnik?.Kategorija == "9")
                {
                    minBase = 0m;
                }

                // Korekcija osnovice po razredu (isti pattern kao u ObracunService)
                decimal brutoOsn = totalBruto;
                if (brutoOsn < minBase / 2m)
                    brutoOsn = minBase / 2m;
                else if (brutoOsn < minBase)
                    brutoOsn = minBase;

                if (maxBase > 0 && brutoOsn > maxBase)
                {
                    brutoOsn = maxBase;
                }

                o.DoprinosPioPoslodavac = Math.Round(brutoOsn * bossPio, 2);
                o.DoprinosZdravstvoPoslodavac = Math.Round(brutoOsn * bossZdr, 2);
                o.DoprinosNezaposlenostPoslodavac = Math.Round(brutoOsn * bossNez, 2);

                _db.Entry(o).State = EntityState.Modified;
                azurirano++;
            }

            await _db.SaveChangesAsync();

            MessageBox.Show(
                $"Uspešno preračunati doprinosi poslodavca za {azurirano} obračuna.",
                "Uspeh", MessageBoxButton.OK, MessageBoxImage.Information);

            StatusMessage.Text = $"Retroaktivno ažurirano {azurirano} obračuna sa doprinosima poslodavca.";
            UcitajPeriodiSummary();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Greška prilikom retroaktivnog preračuna: {ex.Message}", "Greška", MessageBoxButton.OK, MessageBoxImage.Error);
            StatusMessage.Text = "Greška pri retroaktivnom preračunu.";
        }
        finally
        {
            BtnRetroaktivniDoprinosi.IsEnabled = true;
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
    public decimal UkupnoBruto2 { get; set; }
    public DateTime PoslednjiDatum { get; set; }
    public bool Zakljucan { get; set; }

    public bool IsActive => AppConfig.ActiveGodina == Godina && AppConfig.ActiveMesec == Mesec;
}
