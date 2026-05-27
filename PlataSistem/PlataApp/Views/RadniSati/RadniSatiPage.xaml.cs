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
        LoadBulkColumns();
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
                    postojeciObracun.SmenskiSati = rs.SmenskiSati;
                    postojeciObracun.RadPraznikomSati = rs.RadPraznikomSati;
                    postojeciObracun.NocniRadPraznikomSati = rs.NocniRadPraznikomSati;
                    postojeciObracun.PlacenoOdsustvoSati = rs.PlacenoOdsustvoSati;
                    postojeciObracun.NedeljaSati = rs.RadNedeljomSati;
                    postojeciObracun.PlacenoZakonskiSatiLegacy = rs.PlacenoZakonskiSati;
                    postojeciObracun.BolovanjePreko60SatiLegacy = rs.BolovanjePreko60Sati;
                    postojeciObracun.PorodiljskoOdsustvoSatiLegacy = rs.PorodiljskoOdsustvoSati;
                    postojeciObracun.Bolovanje100SatiLegacy = rs.Bolovanje100Sati;

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

    private async void BtnDodajRadnika_Click(object sender, RoutedEventArgs e)
    {
        if (!AppConfig.ActiveGodina.HasValue || !AppConfig.ActiveMesec.HasValue) return;

        int godina = AppConfig.ActiveGodina.Value;
        int mesec = AppConfig.ActiveMesec.Value;

        var dialog = new DodajRadnikaRadniSatiWindow(godina, mesec)
        {
            Owner = Application.Current.MainWindow
        };

        if (dialog.ShowDialog() == true && dialog.SelectedRadnik != null)
        {
            try
            {
                var radnikDialog = dialog.SelectedRadnik;
                var radnik = await _db.Radnici.FindAsync(radnikDialog.Id);
                if (radnik == null) return;
                StatusMessage.Text = $"Dodavanje radnika {radnik.ImeIPrezime}...";

                // Pročitaj parametre perioda za podrazumevane sate i bod
                var porezi = await _db.Porezi.FirstOrDefaultAsync(p => p.Godina == godina && p.Mesec == mesec);
                if (porezi == null)
                {
                    porezi = await _db.Porezi
                        .Where(p => p.Godina < godina || (p.Godina == godina && p.Mesec < mesec))
                        .OrderByDescending(p => p.Godina)
                        .ThenByDescending(p => p.Mesec)
                        .FirstOrDefaultAsync();
                }

                int fondSati = porezi?.FondCasova ?? 176;
                decimal vrednostBoda = porezi?.VrBoda ?? 1860.34m;

                var obracunService = new ObracunService(_db);
                decimal prosek = obracunService.IzracunajProsekRadnika(radnik.Id, godina, mesec);

                var noviSat = new RadniSat
                {
                    RadnikId = radnik.Id,
                    Godina = godina,
                    Mesec = mesec,
                    RedovniSati = fondSati,
                    BolovanjeSati = 0,
                    PrekovremeneSati = 0,
                    GodisnjiOdmorSati = 0,
                    DrzavniPraznikSati = 0,
                    NocniSati = 0,
                    SmenskiSati = 0,
                    RadPraznikomSati = 0,
                    NocniRadPraznikomSati = 0,
                    PlacenoOdsustvoSati = 0,
                    Prosek = prosek
                };

                _db.RadniSati.Add(noviSat);

                // Automatski kreiraj i inicijalni obračun plate za tog radnika da sve bude u sinhronizaciji
                var noviObracun = obracunService.Calculate(radnik, noviSat, godina, mesec, vrednostBoda, fondSati);
                _db.ObracuniPlata.Add(noviObracun);

                await _db.SaveChangesAsync();

                MessageBox.Show(
                    $"Radnik {radnik.ImeIPrezime} je uspešno dodat u evidenciju radnih sati za period {mesec:D2}/{godina}.",
                    "Uspeh",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );

                LoadData();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Greška prilikom dodavanja radnika: {ex.Message}", "Greška", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusMessage.Text = "Došlo je do greške prilikom dodavanja.";
            }
        }
    }

    private async void BtnUkloniRadnika_Click(object sender, RoutedEventArgs e)
    {
        if (!AppConfig.ActiveGodina.HasValue || !AppConfig.ActiveMesec.HasValue) return;

        if (GridRadniSati.SelectedItem is not RadniSat selectedSat)
        {
            MessageBox.Show("Molimo izaberite radnika iz tabele za uklanjanje.", "Upozorenje", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        int godina = AppConfig.ActiveGodina.Value;
        int mesec = AppConfig.ActiveMesec.Value;

        var rez = MessageBox.Show(
            $"Da li zaista želite da uklonite radnika {selectedSat.Radnik.ImeIPrezime} (šifra: {selectedSat.Radnik.BrojRadnika}) iz evidencije radnih sati za tekući period?\n\nOva akcija će takođe obrisati njegov obračun plate za ovaj mesec i vratiti rate njegovih kredita unazad.",
            "Potvrda uklanjanja radnika",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question
        );

        if (rez == MessageBoxResult.No) return;

        try
        {
            StatusMessage.Text = $"Uklanjanje radnika {selectedSat.Radnik.ImeIPrezime}...";

            // 1. Obriši radne sate
            _db.RadniSati.Remove(selectedSat);

            // 2. Pronađi i obriši obračun
            var obracun = await _db.ObracuniPlata
                .FirstOrDefaultAsync(o => o.RadnikId == selectedSat.RadnikId && o.Godina == godina && o.Mesec == mesec);

            if (obracun != null)
            {
                // REVERT: Rate kredita/obustava se vraćaju za 1 mesec unazad
                var targetDate = new DateTime(godina, mesec, 1);
                var radnikKrediti = await _db.Krediti
                    .Where(k => k.RadnikId == selectedSat.RadnikId)
                    .ToListAsync();

                foreach (var k in radnikKrediti)
                {
                    // Ako je ovaj kredit bio otplaćivan u ovom mesecu
                    if (k.DatumPocetka <= targetDate && targetDate <= k.DatumPocetka.AddMonths(k.PlateneRate - 1))
                    {
                        k.PlateneRate--;
                        k.OstatakDuga = Math.Max(0, k.UkupanIznos - (k.PlateneRate * k.MesecnaRata));
                        k.Aktivan = true; // ponovo aktiviramo jer je vraćena rata
                        _db.Entry(k).State = EntityState.Modified;
                    }
                }

                _db.ObracuniPlata.Remove(obracun);
            }

            await _db.SaveChangesAsync();

            MessageBox.Show(
                $"Radnik {selectedSat.Radnik.ImeIPrezime} je uspešno uklonjen iz evidencije.",
                "Uspeh",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );

            LoadData();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Greška prilikom uklanjanja radnika: {ex.Message}", "Greška", MessageBoxButton.OK, MessageBoxImage.Error);
            StatusMessage.Text = "Došlo je do greške prilikom uklanjanja.";
        }
    }

    private void GridRadniSati_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        // Koristimo Dispatcher sa Background prioritetom da sačekamo da se izmena upiše iz UI u model
        Dispatcher.BeginInvoke(new Action(async () =>
        {
            if (e.Row.Item is RadniSat rs)
            {
                await PreracunajRadnika(rs);
            }
        }), System.Windows.Threading.DispatcherPriority.Background);
    }

    private async System.Threading.Tasks.Task PreracunajRadnika(RadniSat rs)
    {
        if (!AppConfig.ActiveGodina.HasValue || !AppConfig.ActiveMesec.HasValue) return;

        int godina = AppConfig.ActiveGodina.Value;
        int mesec = AppConfig.ActiveMesec.Value;

        try
        {
            StatusMessage.Text = "Automatsko preračunavanje plate...";

            // 1. Pročitaj parametre perioda
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

            // Označi radne sate kao modifikovane
            _db.Entry(rs).State = EntityState.Modified;

            // Učitaj radnika iz lokalnog DbContext-a da izbegnemo tracking konflikte
            var radnik = await _db.Radnici.FindAsync(rs.RadnikId);
            if (radnik == null) return;

            // Pronađi postojeći obračun ili kreiraj novi
            var postojeciObracun = await _db.ObracuniPlata
                .FirstOrDefaultAsync(o => o.RadnikId == rs.RadnikId && o.Godina == godina && o.Mesec == mesec);

            var noviObracun = obracunService.Calculate(radnik, rs, godina, mesec, vrednostBoda, fondSati);

            if (postojeciObracun != null)
            {
                // Kopiramo sve obračunate vrednosti
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

                // Kopiraj sate i u obračun
                postojeciObracun.RedovniSati = rs.RedovniSati;
                postojeciObracun.BolovanjeSati = rs.BolovanjeSati;
                postojeciObracun.PrekovremeneSati = rs.PrekovremeneSati;
                postojeciObracun.GodisnjioOdmorSati = rs.GodisnjiOdmorSati;
                postojeciObracun.DrzavniPraznikSati = rs.DrzavniPraznikSati;
                postojeciObracun.NocniSati = rs.NocniSati;
                postojeciObracun.SmenskiSati = rs.SmenskiSati;
                postojeciObracun.RadPraznikomSati = rs.RadPraznikomSati;
                postojeciObracun.NocniRadPraznikomSati = rs.NocniRadPraznikomSati;
                postojeciObracun.PlacenoOdsustvoSati = rs.PlacenoOdsustvoSati;
                postojeciObracun.NedeljaSati = rs.RadNedeljomSati;
                postojeciObracun.PlacenoZakonskiSatiLegacy = rs.PlacenoZakonskiSati;
                postojeciObracun.BolovanjePreko60SatiLegacy = rs.BolovanjePreko60Sati;
                postojeciObracun.PorodiljskoOdsustvoSatiLegacy = rs.PorodiljskoOdsustvoSati;
                postojeciObracun.Bolovanje100SatiLegacy = rs.Bolovanje100Sati;

                postojeciObracun.Prosek = noviObracun.Prosek;
                postojeciObracun.DatumObracuna = DateTime.Now;
                postojeciObracun.Napomena = $"Automatski preračunato nakon izmene sata {DateTime.Now:dd.MM.yyyy HH:mm}";

                _db.Entry(postojeciObracun).State = EntityState.Modified;
            }
            else
            {
                _db.ObracuniPlata.Add(noviObracun);
            }

            await _db.SaveChangesAsync();
            StatusMessage.Text = $"Automatski sačuvano i preračunato za: {radnik.ImeIPrezime} ({DateTime.Now:HH:mm:ss})";
        }
        catch (Exception ex)
        {
            StatusMessage.Text = $"Greška pri automatskom preračunavanju: {ex.Message}";
        }
    }

    private void LoadBulkColumns()
    {
        var list = new List<BulkColumnItem>
        {
            new() { DisplayName = "Redovni sati", PropertyName = nameof(RadniSat.RedovniSati), PropertyType = typeof(int) },
            new() { DisplayName = "Bolovanje", PropertyName = nameof(RadniSat.BolovanjeSati), PropertyType = typeof(int) },
            new() { DisplayName = "Prekovremeni", PropertyName = nameof(RadniSat.PrekovremeneSati), PropertyType = typeof(int) },
            new() { DisplayName = "Godišnji odmor", PropertyName = nameof(RadniSat.GodisnjiOdmorSati), PropertyType = typeof(int) },
            new() { DisplayName = "Državni praznik", PropertyName = nameof(RadniSat.DrzavniPraznikSati), PropertyType = typeof(int) },
            new() { DisplayName = "Noćni rad", PropertyName = nameof(RadniSat.NocniSati), PropertyType = typeof(int) },
            new() { DisplayName = "Smenski rad", PropertyName = nameof(RadniSat.SmenskiSati), PropertyType = typeof(int) },
            new() { DisplayName = "Rad praznikom", PropertyName = nameof(RadniSat.RadPraznikomSati), PropertyType = typeof(int) },
            new() { DisplayName = "Noćni rad praznikom", PropertyName = nameof(RadniSat.NocniRadPraznikomSati), PropertyType = typeof(int) },
            new() { DisplayName = "Plaćeno odsustvo", PropertyName = nameof(RadniSat.PlacenoOdsustvoSati), PropertyType = typeof(int) },
            new() { DisplayName = "Rad nedeljom", PropertyName = nameof(RadniSat.RadNedeljomSati), PropertyType = typeof(int) },
            new() { DisplayName = "Plaćeno zakonski", PropertyName = nameof(RadniSat.PlacenoZakonskiSati), PropertyType = typeof(int) },
            new() { DisplayName = "Bolovanje >60 dana", PropertyName = nameof(RadniSat.BolovanjePreko60Sati), PropertyType = typeof(int) },
            new() { DisplayName = "Porodiljsko", PropertyName = nameof(RadniSat.PorodiljskoOdsustvoSati), PropertyType = typeof(int) },
            new() { DisplayName = "Bolovanje 100%", PropertyName = nameof(RadniSat.Bolovanje100Sati), PropertyType = typeof(int) },
            new() { DisplayName = "Topli obrok (dani)", PropertyName = nameof(RadniSat.TopliObrokDani), PropertyType = typeof(int) },
            new() { DisplayName = "Regres (iznos)", PropertyName = nameof(RadniSat.RegresIznos), PropertyType = typeof(decimal) },
            new() { DisplayName = "Stimulacija (%)", PropertyName = nameof(RadniSat.Stimulacija), PropertyType = typeof(decimal) },
            new() { DisplayName = "Prosek (12m)", PropertyName = nameof(RadniSat.Prosek), PropertyType = typeof(decimal) }
        };
        ComboBulkKolona.ItemsSource = list;
        ComboBulkKolona.SelectedIndex = 0;
    }

    private async void BtnPrimeniNaSve_Click(object sender, RoutedEventArgs e)
    {
        if (ComboBulkKolona.SelectedItem is not BulkColumnItem selectedItem) return;
        if (string.IsNullOrWhiteSpace(TxtBulkValue.Text))
        {
            MessageBox.Show("Molimo unesite vrednost koju želite da primenite.", "Upozorenje", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        string valStr = TxtBulkValue.Text.Trim();
        object valueToSet;
        if (selectedItem.PropertyType == typeof(int))
        {
            if (!int.TryParse(valStr, out int valInt) || valInt < 0)
            {
                MessageBox.Show("Molimo unesite ispravan ceo broj za izabranu kolonu.", "Upozorenje", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            valueToSet = valInt;
        }
        else
        {
            if (!decimal.TryParse(valStr, out decimal valDec) || valDec < 0)
            {
                MessageBox.Show("Molimo unesite ispravan decimalni broj za izabranu kolonu.", "Upozorenje", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            valueToSet = valDec;
        }

        var rez = MessageBox.Show(
            $"Da li zaista želite da popunite kolonu '{selectedItem.DisplayName}' svim radnicima vrednošću '{valStr}'?\n\nOva akcija će automatski ažurirati i preračunati plate svih zaposlenih u tekućem periodu.",
            "Potvrda masovne izmene",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question
        );
        if (rez == MessageBoxResult.No) return;

        try
        {
            StatusMessage.Text = "Ažuriranje i preračunavanje svih radnika...";
            ActionBarButtons.IsEnabled = false;
            GridRadniSati.IsEnabled = false;

            // Učitaj parametre perioda
            int godina = AppConfig.ActiveGodina!.Value;
            int mesec = AppConfig.ActiveMesec!.Value;

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
            var prop = typeof(RadniSat).GetProperty(selectedItem.PropertyName);

            foreach (var rs in _allSati)
            {
                prop?.SetValue(rs, valueToSet);
                _db.Entry(rs).State = EntityState.Modified;

                var radnik = await _db.Radnici.FindAsync(rs.RadnikId);
                if (radnik == null) continue;

                var postojeciObracun = await _db.ObracuniPlata
                    .FirstOrDefaultAsync(o => o.RadnikId == rs.RadnikId && o.Godina == godina && o.Mesec == mesec);

                var noviObracun = obracunService.Calculate(radnik, rs, godina, mesec, vrednostBoda, fondSati);

                if (postojeciObracun != null)
                {
                    // Kopiramo vrednosti obračuna
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

                    // Kopiraj sate i u obračun
                    postojeciObracun.RedovniSati = rs.RedovniSati;
                    postojeciObracun.BolovanjeSati = rs.BolovanjeSati;
                    postojeciObracun.PrekovremeneSati = rs.PrekovremeneSati;
                    postojeciObracun.GodisnjioOdmorSati = rs.GodisnjiOdmorSati;
                    postojeciObracun.DrzavniPraznikSati = rs.DrzavniPraznikSati;
                    postojeciObracun.NocniSati = rs.NocniSati;
                    postojeciObracun.SmenskiSati = rs.SmenskiSati;
                    postojeciObracun.RadPraznikomSati = rs.RadPraznikomSati;
                    postojeciObracun.NocniRadPraznikomSati = rs.NocniRadPraznikomSati;
                    postojeciObracun.PlacenoOdsustvoSati = rs.PlacenoOdsustvoSati;
                    postojeciObracun.NedeljaSati = rs.RadNedeljomSati;
                    postojeciObracun.PlacenoZakonskiSatiLegacy = rs.PlacenoZakonskiSati;
                    postojeciObracun.BolovanjePreko60SatiLegacy = rs.BolovanjePreko60Sati;
                    postojeciObracun.PorodiljskoOdsustvoSatiLegacy = rs.PorodiljskoOdsustvoSati;
                    postojeciObracun.Bolovanje100SatiLegacy = rs.Bolovanje100Sati;

                    postojeciObracun.Prosek = noviObracun.Prosek;
                    postojeciObracun.DatumObracuna = DateTime.Now;
                    postojeciObracun.Napomena = $"Automatski preračunato nakon masovne izmene {DateTime.Now:dd.MM.yyyy HH:mm}";

                    _db.Entry(postojeciObracun).State = EntityState.Modified;
                }
                else
                {
                    _db.ObracuniPlata.Add(noviObracun);
                }
            }

            await _db.SaveChangesAsync();
            GridRadniSati.Items.Refresh();

            MessageBox.Show(
                $"Uspešno popunjena kolona '{selectedItem.DisplayName}' sa vrednošću '{valStr}' za sve radnike ({_allSati.Count} zapisa) i preračunate plate.",
                "Uspeh",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );

            LoadData();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Greška prilikom masovne izmene: {ex.Message}", "Greška", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            ActionBarButtons.IsEnabled = true;
            GridRadniSati.IsEnabled = true;
        }
    }
}

public class BulkColumnItem
{
    public string DisplayName { get; set; } = "";
    public string PropertyName { get; set; } = "";
    public Type PropertyType { get; set; } = typeof(int);
}

