using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;
using PlataData;
using PlataData.Models;
using PlataApp.Services;
using PlataApp.Views.Obracuni;

namespace PlataApp.Views.RadniSati;

public partial class RadniSatiPage : Page
{
    private PlataDbContext _db;
    private List<RadniSat> _allSati = new();

    public RadniSatiPage()
    {
        InitializeComponent();
        _db = PlataDbContext.Create(AppConfig.DbPath);
        LoadData();
    }

    private void LoadData()
    {
        try
        {
            if (!AppConfig.ActiveGodina.HasValue || !AppConfig.ActiveMesec.HasValue)
            {
                WarningBorder.Visibility = Visibility.Visible;
                ToolbarBorder.Visibility = Visibility.Collapsed;
                GridBorder.Visibility = Visibility.Collapsed;
                ActionBarButtons.Visibility = Visibility.Collapsed;
                StatusMessage.Text = "Nije izabran aktivni period.";
                return;
            }

            int godina = AppConfig.ActiveGodina.Value;
            int mesec = AppConfig.ActiveMesec.Value;

            string[] meseciStr = {
                "Januar", "Februar", "Mart", "April", "Maj", "Jun",
                "Jul", "Avgust", "Septembar", "Oktobar", "Novembar", "Decembar"
            };
            string periodNaziv = mesec >= 1 && mesec <= 12 ? $"{meseciStr[mesec - 1]} {godina}" : $"{mesec:D2}/{godina}";
            ActivePeriodSubtitle.Text = $"Uređivanje radnih sati zaposlenih u aktivnom periodu: {periodNaziv}";

            // Učitaj parametre perioda iz baze (tabela Porezi)
            var porezi = _db.Porezi.FirstOrDefault(p => p.Godina == godina && p.Mesec == mesec);
            if (porezi == null)
            {
                porezi = _db.Porezi
                    .Where(p => p.Godina < godina || (p.Godina == godina && p.Mesec < mesec))
                    .OrderByDescending(p => p.Godina)
                    .ThenByDescending(p => p.Mesec)
                    .FirstOrDefault();
            }

            decimal vrednostBoda = porezi?.VrBoda ?? 1860.34m;
            int fondSati = porezi?.FondCasova ?? 176;

            TxtVrednostBoda.Text = $"{vrednostBoda:N2} RSD";
            TxtFondCasova.Text = $"{fondSati} č";

            // Učitaj radne sate za aktivni period
            _allSati = _db.RadniSati
                .Include(s => s.Radnik)
                .Where(s => s.Godina == godina && s.Mesec == mesec)
                .OrderBy(s => s.Radnik.BrojRadnika)
                .ToList();

            WarningBorder.Visibility = Visibility.Collapsed;
            ToolbarBorder.Visibility = Visibility.Visible;
            GridBorder.Visibility = Visibility.Visible;
            ActionBarButtons.Visibility = Visibility.Visible;

            FilterList();

            StatusMessage.Text = $"Pronađeno {_allSati.Count} zapisa o radnim satima zaposlenih za period {periodNaziv}.";
        }
        catch (Exception ex)
        {
            StatusMessage.Text = $"Greška pri učitavanju podataka: {ex.Message}";
        }
    }

    private void FilterList()
    {
        string filter = SearchBox.Text.Trim().ToLower();
        SearchPlaceholder.Visibility = string.IsNullOrEmpty(SearchBox.Text) ? Visibility.Visible : Visibility.Collapsed;

        if (string.IsNullOrEmpty(filter))
        {
            GridRadniSati.ItemsSource = _allSati;
        }
        else
        {
            GridRadniSati.ItemsSource = _allSati
                .Where(s => s.Radnik.ImeIPrezime.ToLower().Contains(filter))
                .ToList();
        }
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        FilterList();
    }

    private void BtnIdiNaPocetnu_Click(object sender, RoutedEventArgs e)
    {
        var mainWin = Application.Current.MainWindow as MainWindow;
        mainWin?.MainFrame.Navigate(new ObracuniPage());
    }

    private void BtnOsvezi_Click(object sender, RoutedEventArgs e)
    {
        // Ponovo učitaj iz baze, time se odbacuju sve nesnimljene promene
        _db = PlataDbContext.Create(AppConfig.DbPath);
        LoadData();
        StatusMessage.Text = "Podaci su osveženi iz baze.";
    }

    private async void BtnSacuvaj_Click(object sender, RoutedEventArgs e)
    {
        if (!AppConfig.ActiveGodina.HasValue || !AppConfig.ActiveMesec.HasValue) return;

        int godina = AppConfig.ActiveGodina.Value;
        int mesec = AppConfig.ActiveMesec.Value;

        StatusMessage.Text = "Čuvanje i preračunavanje plata... Molimo sačekajte.";
        ActionBarButtons.IsEnabled = false;
        GridRadniSati.IsEnabled = false;

        try
        {
            // Pročitaj parametre za obračun
            var porezi = await _db.Porezi.FirstOrDefaultAsync(p => p.Godina == godina && p.Mesec == mesec);
            if (porezi == null)
            {
                porezi = await _db.Porezi
                    .Where(p => p.Godina < godina || (p.Godina == godina && p.Mesec < mesec))
                    .OrderByDescending(p => p.Godina)
                    .ThenByDescending(p => p.Mesec)
                    .FirstOrDefaultAsync();
            }

            decimal vrednostBoda = porezi?.VrBoda ?? 1860.34m;
            int fondSati = porezi?.FondCasova ?? 176;

            var obracunService = new ObracunService(_db);
            int updatedCount = 0;

            foreach (var rs in _allSati)
            {
                // Označi sate kao modifikovane
                _db.Entry(rs).State = EntityState.Modified;

                // Ponovo preračunaj obračun plate za ovog radnika
                var radnik = rs.Radnik;
                var postojeciObracun = await _db.ObracuniPlata
                    .FirstOrDefaultAsync(o => o.RadnikId == rs.RadnikId && o.Godina == godina && o.Mesec == mesec);

                if (postojeciObracun != null)
                {
                    var noviObracun = obracunService.Calculate(radnik, rs, godina, mesec, vrednostBoda, fondSati);

                    // Kopiramo vrednosti da bi EF Core ispratio izmene nad istim entitetom
                    postojeciObracun.BrutoZarada = noviObracun.BrutoZarada;
                    postojeciObracun.BrutoBolovanje = noviObracun.BrutoBolovanje;
                    postojeciObracun.BrutoNaknade = noviObracun.BrutoNaknade;
                    postojeciObracun.BrutoStimulacija = noviObracun.BrutoStimulacija;
                    postojeciObracun.BrutoMinuliRad = noviObracun.BrutoMinuliRad;

                    postojeciObracun.NetoZar = noviObracun.NetoZar;
                    postojeciObracun.NetoNerd = noviObracun.NetoNerd;
                    postojeciObracun.NetoGOd = noviObracun.NetoGOd;
                    postojeciObracun.NetoTo = noviObracun.NetoTo;
                    postojeciObracun.NetoReg = noviObracun.NetoReg;
                    postojeciObracun.Neto = noviObracun.Neto;
                    postojeciObracun.NetoBol = noviObracun.NetoBol;
                    postojeciObracun.NetoB100 = noviObracun.NetoB100;
                    postojeciObracun.NetoPlac = noviObracun.NetoPlac;
                    postojeciObracun.NetoPlZ = noviObracun.NetoPlZ;
                    postojeciObracun.NetoDrza = noviObracun.NetoDrza;
                    postojeciObracun.NetoNocni = noviObracun.NetoNocni;
                    postojeciObracun.NetoVezba = noviObracun.NetoVezba;
                    postojeciObracun.NetoPrek = noviObracun.NetoPrek;
                    postojeciObracun.NetoTer = noviObracun.NetoTer;
                    postojeciObracun.KorDod = noviObracun.KorDod;
                    postojeciObracun.KorDod1 = noviObracun.KorDod1;
                    postojeciObracun.Kumul = noviObracun.Kumul;
                    postojeciObracun.NetoNede = noviObracun.NetoNede;

                    postojeciObracun.DoprinosPioRadnik = noviObracun.DoprinosPioRadnik;
                    postojeciObracun.DoprinosZdravstvoRadnik = noviObracun.DoprinosZdravstvoRadnik;
                    postojeciObracun.DoprinosNezaposlenostRadnik = noviObracun.DoprinosNezaposlenostRadnik;

                    postojeciObracun.DoprinosPioPoslodavac = noviObracun.DoprinosPioPoslodavac;
                    postojeciObracun.DoprinosZdravstvoPoslodavac = noviObracun.DoprinosZdravstvoPoslodavac;
                    postojeciObracun.DoprinosNezaposlenostPoslodavac = noviObracun.DoprinosNezaposlenostPoslodavac;

                    postojeciObracun.PorezNaDohodak = noviObracun.PorezNaDohodak;
                    postojeciObracun.PoreskaOsnovica = noviObracun.PoreskaOsnovica;
                    postojeciObracun.LicniOdbitak = noviObracun.LicniOdbitak;
                    postojeciObracun.KreditObustava = noviObracun.KreditObustava;
                    postojeciObracun.Samodoprinosi = noviObracun.Samodoprinosi;
                    postojeciObracun.OstaliOdbici = noviObracun.OstaliOdbici;
                    postojeciObracun.NetoIsplata = noviObracun.NetoIsplata;

                    // Obezbedimo da su svi sati preneti i u istorijski obračun
                    postojeciObracun.RedovniSati = rs.RedovniSati;
                    postojeciObracun.BolovanjeSati = rs.BolovanjeSati;
                    postojeciObracun.PrekovremeneSati = rs.PrekovremeneSati;
                    postojeciObracun.GodisnjioOdmorSati = rs.GodisnjiOdmorSati;
                    postojeciObracun.DrzavniPraznikSati = rs.DrzavniPraznikSati;
                    postojeciObracun.NocniSati = rs.NocniSati;

                    postojeciObracun.Prosek = noviObracun.Prosek;
                    postojeciObracun.DatumObracuna = DateTime.Now;
                    postojeciObracun.Napomena = $"Ažurirano izmenom radnih sati {DateTime.Now:dd.MM.yyyy HH:mm}";

                    _db.Entry(postojeciObracun).State = EntityState.Modified;
                }
                else
                {
                    // Ako nema obračuna, kreiramo ga
                    var noviObracun = obracunService.Calculate(radnik, rs, godina, mesec, vrednostBoda, fondSati);
                    _db.ObracuniPlata.Add(noviObracun);
                }

                updatedCount++;
            }

            await _db.SaveChangesAsync();

            MessageBox.Show(
                $"Uspešno sačuvani radni sati i preračunate plate za {updatedCount} zaposlenih u periodu {mesec:D2}/{godina}.",
                "Uspeh",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );

            LoadData(); // reload za najnovije prosek/meta podatke
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Greška prilikom snimanja i preračunavanja: {ex.Message}", "Greška", MessageBoxButton.OK, MessageBoxImage.Error);
            StatusMessage.Text = "Došlo je do greške prilikom čuvanja.";
        }
        finally
        {
            ActionBarButtons.IsEnabled = true;
            GridRadniSati.IsEnabled = true;
        }
    }
}
