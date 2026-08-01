using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using PlataData;
using PlataData.Models;
using PlataApp.Services;
using PlataApp.Views.Obracuni;

namespace PlataApp.Views.Obracun;

public partial class NoviObracunWindow : Window
{
    private readonly PlataDbContext _db;
    private readonly ObracunService _obracunService;
    private readonly ObracunPeriodSummary? _preselectedPeriod;
    private ObservableCollection<RadnikSatiInput> _radniciSati = [];

    public NoviObracunWindow(ObracunPeriodSummary? preselectedPeriod = null)
    {
        InitializeComponent();
        Views.Pomoc.ContextHelpFix.UkloniDugmeZaPomoc(this);
        KeyDown += (s, e) => { if (e.Key == System.Windows.Input.Key.F1) { new Views.Pomoc.EditHelpWindow("Novi obračun zarada", "Mesečni obračun plata i naknada", new[] { ("F1", "Pomoć"), ("Esc", "Zatvori prozor") }, "Unesite godinu, mesec, vrednost boda i fond časova.").ShowDialog(); e.Handled = true; } };
        
        _preselectedPeriod = preselectedPeriod;
        _db = PlataDbContext.Create(AppConfig.DbPath);
        _obracunService = new ObracunService(_db);

        // Učitaj sve postojeće periode za prenos podataka
        UcitajPeriodeZaPrenos();

        // Inicijalizuj ComboBox-eve
        ComboGodina.ItemsSource = Enumerable.Range(DateTime.Now.Year - 10, 12).OrderByDescending(g => g).ToList();
        ComboMesec.ItemsSource = Enumerable.Range(1, 12).ToList();

        if (_preselectedPeriod != null)
        {
            // Ako je izabran obračun, ponudi sledeći mesec za novi obračun
            int sledeciMesec = _preselectedPeriod.Mesec + 1;
            int sledecaGodina = _preselectedPeriod.Godina;
            if (sledeciMesec > 12)
            {
                sledeciMesec = 1;
                sledecaGodina++;
            }
            ComboGodina.SelectedItem = sledecaGodina;
            ComboMesec.SelectedItem = sledeciMesec;
            
            // A za prenos ponudi upravo taj izabrani obračun!
            PostaviSelektovaniPeriodZaPrenos(_preselectedPeriod.Godina, _preselectedPeriod.Mesec);
        }
        else
        {
            ComboGodina.SelectedItem = DateTime.Now.Month == 1 ? DateTime.Now.Year - 1 : DateTime.Now.Year;
            ComboMesec.SelectedItem = DateTime.Now.Month == 1 ? 12 : DateTime.Now.Month - 1;
            
            // Ponudi poslednji obračun za prenos
            PostaviPoslednjiPeriodZaPrenos();
        }

        LoadAktivneRadnike();

        ComboGodina.SelectionChanged += ComboPeriod_SelectionChanged;
        ComboMesec.SelectionChanged += ComboPeriod_SelectionChanged;
    }

    private void UcitajPeriodeZaPrenos()
    {
        try
        {
            var uniquePeriods = _db.RadniSati
                .Select(s => new { s.Godina, s.Mesec })
                .Distinct()
                .OrderByDescending(p => p.Godina)
                .ThenByDescending(p => p.Mesec)
                .ToList();

            var list = uniquePeriods.Select(p => new PrenosPeriodItem
            {
                Godina = p.Godina,
                Mesec = p.Mesec
            }).ToList();

            ComboPrenosIz.ItemsSource = list;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Greška pri učitavanju perioda za prenos: {ex.Message}", "Greška", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void PostaviSelektovaniPeriodZaPrenos(int godina, int mesec)
    {
        if (ComboPrenosIz.ItemsSource is List<PrenosPeriodItem> items)
        {
            var target = items.FirstOrDefault(i => i.Godina == godina && i.Mesec == mesec);
            if (target != null)
            {
                ComboPrenosIz.SelectedItem = target;
            }
        }
    }

    private void PostaviPoslednjiPeriodZaPrenos()
    {
        if (ComboPrenosIz.ItemsSource is List<PrenosPeriodItem> items && items.Count > 0)
        {
            ComboPrenosIz.SelectedIndex = 0;
        }
    }

    private void ComboPeriod_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        LoadAktivneRadnike();
    }

    private void LoadAktivneRadnike()
    {
        try
        {
            if (ComboGodina.SelectedItem == null || ComboMesec.SelectedItem == null)
                return;

            int godina = (int)ComboGodina.SelectedItem;
            int mesec = (int)ComboMesec.SelectedItem;

            // Pronađi stope i parametre za izabrani period iz baze (sa fallback-om na najbliži prethodni)
            var porezi = _db.Porezi
                .FirstOrDefault(p => p.Godina == godina && p.Mesec == mesec);
            if (porezi == null)
            {
                porezi = _db.Porezi
                    .Where(p => p.Godina < godina || (p.Godina == godina && p.Mesec < mesec))
                    .OrderByDescending(p => p.Godina)
                    .ThenByDescending(p => p.Mesec)
                    .FirstOrDefault();
            }

            decimal vrBoda = porezi?.VrBoda ?? 1860.34m;
            int fondIzBaze = porezi?.FondCasova ?? 176;

            TxtVrednostBoda.Text = vrBoda.ToString("F4");
            TxtFondCasova.Text = fondIzBaze.ToString();
            int fondSati = fondIzBaze;

            // 1. Ako nema radnika u ciljnom periodu, automatski ih kopiramo iz najbližeg prethodnog
            var imaTargetRadnika = _db.Radnici.Any(r => r.Godina == godina && r.Mesec == mesec);
            if (!imaTargetRadnika)
            {
                var sourcePeriod = _db.Radnici
                    .OrderByDescending(r => r.Godina)
                    .ThenByDescending(r => r.Mesec)
                    .Select(r => new { r.Godina, r.Mesec })
                    .FirstOrDefault();

                if (sourcePeriod != null)
                {
                    KopirajRadnikeIzPerioda(sourcePeriod.Godina, sourcePeriod.Mesec, godina, mesec);
                }
            }

            var aktivniRadnici = _db.Radnici
                .Where(r => r.Aktivan && r.Godina == godina && r.Mesec == mesec)
                .ToList();

            var postojeciSati = _db.RadniSati
                .Where(s => s.Godina == godina && s.Mesec == mesec)
                .ToDictionary(s => s.RadnikId);

            // Uključujemo i neaktivne radnike koji imaju istorijske podatke za ovaj period
            var radnikIdsSaSacuvanim = postojeciSati.Keys.ToList();
            var sviRelevantniRadnici = aktivniRadnici;
            if (radnikIdsSaSacuvanim.Count > 0)
            {
                var dodatniRadnici = _db.Radnici
                    .Where(r => !r.Aktivan && r.Godina == godina && r.Mesec == mesec && radnikIdsSaSacuvanim.Contains(r.Id))
                    .ToList();

                sviRelevantniRadnici = aktivniRadnici.Concat(dodatniRadnici)
                    .DistinctBy(r => r.Id)
                    .OrderBy(r => r.BrojRadnika)
                    .ToList();
            }
            else
            {
                sviRelevantniRadnici = sviRelevantniRadnici.OrderBy(r => r.BrojRadnika).ToList();
            }

            var list = sviRelevantniRadnici.Select(r =>
            {
                if (postojeciSati.TryGetValue(r.Id, out var sacuvaniSati))
                {
                    // Prosek se uvek iznova izračunava za ciljni period — ne prenosi se
                    return new RadnikSatiInput
                    {
                        RadnikId = r.Id,
                        BrojRadnika = r.BrojRadnika,
                        ImeIPrezime = r.ImeIPrezime,
                        Koeficijent = r.Koeficijent,
                        RedovniSati = sacuvaniSati.RedovniSati,
                        BolovanjeSati = sacuvaniSati.BolovanjeSati,
                        PrekovremeneSati = sacuvaniSati.PrekovremeneSati,
                        GodisnjiOdmorSati = sacuvaniSati.GodisnjiOdmorSati,
                        DrzavniPraznikSati = sacuvaniSati.DrzavniPraznikSati,
                        NocniSati = sacuvaniSati.NocniSati,
                        SmenskiSati = sacuvaniSati.SmenskiSati,
                        RadPraznikomSati = sacuvaniSati.RadPraznikomSati,
                        NocniRadPraznikomSati = sacuvaniSati.NocniRadPraznikomSati,
                        PlacenoOdsustvoSati = sacuvaniSati.PlacenoOdsustvoSati,
                        Stimulacija = sacuvaniSati.Stimulacija,
                        RadNedeljomSati = sacuvaniSati.RadNedeljomSati,
                        PlacenoZakonskiSati = sacuvaniSati.PlacenoZakonskiSati,
                        BolovanjePreko60Sati = sacuvaniSati.BolovanjePreko60Sati,
                        PorodiljskoOdsustvoSati = sacuvaniSati.PorodiljskoOdsustvoSati,
                        Bolovanje100Sati = sacuvaniSati.Bolovanje100Sati,
                        TopliObrokDani = sacuvaniSati.TopliObrokDani,
                        RegresIznos = sacuvaniSati.RegresIznos,
                        Varijabila = sacuvaniSati.Varijabila,
                        Prosek = _obracunService.IzracunajProsekRadnika(r.Id, godina, mesec)
                    };
                }
                else
                {
                    decimal prosek = _obracunService.IzracunajProsekRadnika(r.Id, godina, mesec);
                    return new RadnikSatiInput
                    {
                        RadnikId = r.Id,
                        BrojRadnika = r.BrojRadnika,
                        ImeIPrezime = r.ImeIPrezime,
                        Koeficijent = r.Koeficijent,
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
                        Stimulacija = 0,
                        RadNedeljomSati = 0,
                        PlacenoZakonskiSati = 0,
                        BolovanjePreko60Sati = 0,
                        PorodiljskoOdsustvoSati = 0,
                        Bolovanje100Sati = 0,
                        TopliObrokDani = 0,
                        RegresIznos = 0,
                        Varijabila = 0,
                        Prosek = prosek
                    };
                }
            }).ToList();

            _radniciSati = new ObservableCollection<RadnikSatiInput>(list);
            GridRadniciSati.ItemsSource = _radniciSati;
            
            if (postojeciSati.Count > 0)
            {
                TxtObavestenje.Text = $"📂 Učitani postojeći radni sati ({postojeciSati.Count} zapisa) za period {mesec}.{godina}.";
            }
            else
            {
                TxtObavestenje.Text = $"🆕 Nema sačuvanih sati za period {mesec}.{godina}. Učitane su podrazumevane vrednosti.";
            }

            // Proveri da li je period zaključan
            bool isLocked = _db.ObracuniPlata.Any(o => o.Godina == godina && o.Mesec == mesec && o.Zakljucan);
            if (isLocked)
            {
                TxtObavestenje.Text = "🔒 Ovaj obračunski period je zaključan. Nisu dozvoljene izmene.";
                TxtObavestenje.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(220, 38, 38)); // Crvena
                BtnSacuvaj.IsEnabled = false;
                BtnResetuj.IsEnabled = false;
                GridRadniciSati.IsReadOnly = true;
            }
            else
            {
                TxtObavestenje.Foreground = (System.Windows.Media.Brush)FindResource("TextSecondaryBrush");
                BtnSacuvaj.IsEnabled = true;
                BtnResetuj.IsEnabled = true;
                GridRadniciSati.IsReadOnly = false;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Greška prilikom učitavanja radnika: {ex.Message}", "Greška", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void TxtFondCasova_LostFocus(object sender, RoutedEventArgs e)
    {
        if (int.TryParse(TxtFondCasova.Text, out int fondSati) && fondSati > 0)
        {
            // Ažuriraj redovne sate kod radnika koji imaju podrazumevani fond
            foreach (var r in _radniciSati)
            {
                r.RedovniSati = fondSati;
            }
            GridRadniciSati.Items.Refresh();
        }
    }

    private void BtnOtkazi_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private async void BtnIzracunaj_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Validacije parametara
            if (ComboGodina.SelectedItem == null || ComboMesec.SelectedItem == null)
            {
                MessageBox.Show("Molimo izaberite godinu i mesec.", "Upozorenje", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int godina = (int)ComboGodina.SelectedItem;
            int mesec = (int)ComboMesec.SelectedItem;

            if (!decimal.TryParse(TxtVrednostBoda.Text, out decimal vrednostBoda) || vrednostBoda <= 0)
            {
                MessageBox.Show("Molimo unesite ispravnu vrednost boda.", "Upozorenje", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(TxtFondCasova.Text, out int fondSati) || fondSati <= 0)
            {
                MessageBox.Show("Molimo unesite ispravan fond časova.", "Upozorenje", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Osiguraj poreske, doprinosne i bankovne parametre za ciljni mesec (bilo prenosom ili automatskim fallback kloniranjem)
            await OsigurajParametreZaCiljniMesecAsync(godina, mesec, vrednostBoda, fondSati);

            // Provera da li već postoje obračuni za ovaj period
            var postojeci = await _db.ObracuniPlata
                .Where(o => o.Godina == godina && o.Mesec == mesec)
                .ToListAsync();

            if (postojeci.Count > 0)
            {
                var rez = MessageBox.Show(
                    $"Već postoje obračuni ({postojeci.Count}) za period {mesec}.{godina}. Da li želite da ih obrišete i pokrenete novi obračun?",
                    "Potvrda brisanja i ponovnog obračuna",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (rez == MessageBoxResult.No) return;

                // REVERT: Pre nego što obrišemo stare obračune, moramo da vratimo rate kredita
                var targetDate = new DateTime(godina, mesec, 1);
                foreach (var obracun in postojeci)
                {
                    var radnikKrediti = await _db.Krediti
                        .Where(k => k.RadnikId == obracun.RadnikId)
                        .ToListAsync();

                    foreach (var k in radnikKrediti)
                    {
                        // Proveravamo da li je ovaj kredit bio otplaćivan u ovom mesecu
                        // (tj. da li je mesec u opsegu [DatumPocetka, DatumPocetka + PlateneRate - 1])
                        if (k.DatumPocetka <= targetDate && targetDate <= k.DatumPocetka.AddMonths(k.PlateneRate - 1))
                        {
                            k.PlateneRate--;
                            k.OstatakDuga = Math.Max(0, k.UkupanIznos - (k.PlateneRate * k.MesecnaRata));
                            k.Aktivan = true; // ponovo ga aktiviramo jer smo mu vratili ratu
                            _db.Entry(k).State = EntityState.Modified;
                        }
                    }
                }

                // Obriši postojeće
                _db.ObracuniPlata.RemoveRange(postojeci);
                await _db.SaveChangesAsync();
            }

            TxtObavestenje.Text = "Obračunavam zarade... Molimo sačekajte.";
            
            int calculatedCount = 0;
            var targetDateNew = new DateTime(godina, mesec, 1);

            foreach (var input in _radniciSati)
            {
                var radnik = await _db.Radnici.FindAsync(input.RadnikId);
                if (radnik == null) continue;

                var radniSati = new RadniSat
                {
                    RadnikId = input.RadnikId,
                    Godina = godina,
                    Mesec = mesec,
                    RedovniSati = input.RedovniSati,
                    BolovanjeSati = input.BolovanjeSati,
                    PrekovremeneSati = input.PrekovremeneSati,
                    GodisnjiOdmorSati = input.GodisnjiOdmorSati,
                    DrzavniPraznikSati = input.DrzavniPraznikSati,
                    NocniSati = input.NocniSati,
                    SmenskiSati = input.SmenskiSati,
                    RadPraznikomSati = input.RadPraznikomSati,
                    NocniRadPraznikomSati = input.NocniRadPraznikomSati,
                    PlacenoOdsustvoSati = input.PlacenoOdsustvoSati,
                    Stimulacija = input.Stimulacija,
                    RadNedeljomSati = input.RadNedeljomSati,
                    PlacenoZakonskiSati = input.PlacenoZakonskiSati,
                    BolovanjePreko60Sati = input.BolovanjePreko60Sati,
                    PorodiljskoOdsustvoSati = input.PorodiljskoOdsustvoSati,
                    Bolovanje100Sati = input.Bolovanje100Sati,
                    TopliObrokDani = input.TopliObrokDani,
                    RegresIznos = input.RegresIznos,
                    Varijabila = input.Varijabila,
                    Prosek = input.Prosek
                };

                // Dodaj i u bazu radnih sati ako ne postoji
                var postojeciSati = await _db.RadniSati
                    .FirstOrDefaultAsync(s => s.RadnikId == input.RadnikId && s.Godina == godina && s.Mesec == mesec);
                if (postojeciSati != null)
                {
                    _db.RadniSati.Remove(postojeciSati);
                }
                _db.RadniSati.Add(radniSati);

                // Izračunaj platu
                var obracun = _obracunService.Calculate(radnik, radniSati, godina, mesec, vrednostBoda, fondSati);
                _db.ObracuniPlata.Add(obracun);

                // DEDUCT: Smanji ostatak duga i uvećaj plaćene rate za aktivne kredite radnika u ovom mesecu
                var activeKrediti = await _db.Krediti
                    .Where(k => k.RadnikId == radnik.Id && k.Aktivan && k.DatumPocetka <= targetDateNew)
                    .ToListAsync();

                foreach (var k in activeKrediti)
                {
                    decimal rata = Math.Min(k.MesecnaRata, k.OstatakDuga);
                    if (rata > 0)
                    {
                        k.PlateneRate++;
                        k.OstatakDuga = Math.Max(0, k.UkupanIznos - (k.PlateneRate * k.MesecnaRata));
                        if (k.OstatakDuga <= 0 || k.PlateneRate >= k.BrojRata)
                        {
                            k.Aktivan = false;
                        }
                        _db.Entry(k).State = EntityState.Modified;
                    }
                }

                calculatedCount++;
            }

            await _db.SaveChangesAsync();

            // Aktivirati novi mesec
            AppConfig.ActiveGodina = godina;
            AppConfig.ActiveMesec = mesec;

            var mainWindow = Application.Current.MainWindow as MainWindow;
            mainWindow?.OsveziAktivniPeriodPrikaz();

            MessageBox.Show($"Uspešno obračunate plate za {calculatedCount} radnika za period {mesec}.{godina}.", "Uspeh", MessageBoxButton.OK, MessageBoxImage.Information);
            
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Greška prilikom obračuna: {ex.Message}", "Greška", MessageBoxButton.OK, MessageBoxImage.Error);
            TxtObavestenje.Text = "Došlo je do greške tokom obračuna.";
        }
    }

    private void BtnResetuj_Click(object sender, RoutedEventArgs e)
    {
        if (ComboGodina.SelectedItem == null || ComboMesec.SelectedItem == null) return;
        
        int godina = (int)ComboGodina.SelectedItem;
        int mesec = (int)ComboMesec.SelectedItem;
        
        var rez = MessageBox.Show(
            $"⚠️ PAŽNJA: Da li zaista želite da obrišete sve unete radne sate za period {mesec}.{godina} i resetujete ih na podrazumevane vrednosti?",
            "Potvrda resetovanja radnih sati",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
            
        if (rez == MessageBoxResult.No) return;
        
        try
        {
            if (!int.TryParse(TxtFondCasova.Text, out int fondSati))
                fondSati = 176;
                
            var aktivniRadnici = _db.Radnici
                .Where(r => r.Aktivan)
                .OrderBy(r => r.BrojRadnika)
                .ToList();
                
            var list = aktivniRadnici.Select(r =>
            {
                decimal prosek = _obracunService.IzracunajProsekRadnika(r.Id, godina, mesec);
                return new RadnikSatiInput
                {
                    RadnikId = r.Id,
                    BrojRadnika = r.BrojRadnika,
                    ImeIPrezime = r.ImeIPrezime,
                    Koeficijent = r.Koeficijent,
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
                    Stimulacija = 0,
                    RadNedeljomSati = 0,
                    PlacenoZakonskiSati = 0,
                    BolovanjePreko60Sati = 0,
                    PorodiljskoOdsustvoSati = 0,
                    Bolovanje100Sati = 0,
                    TopliObrokDani = 0,
                    RegresIznos = 0,
                    Varijabila = 0,
                    Prosek = prosek
                };
            }).ToList();
            
            _radniciSati = new ObservableCollection<RadnikSatiInput>(list);
            GridRadniciSati.ItemsSource = _radniciSati;
            
            TxtObavestenje.Text = $"🔄 Sati su resetovani na podrazumevane vrednosti za period {mesec}.{godina}. Sačuvajte izmene klikom na 'Izračunaj i sačuvaj'.";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Greška prilikom resetovanja sati: {ex.Message}", "Greška", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void BtnPrenesi_Click(object sender, RoutedEventArgs e)
    {
        if (ComboPrenosIz.SelectedItem is not PrenosPeriodItem selectedSource)
        {
            MessageBox.Show("Molimo izaberite period iz kojeg želite da prenesete podatke o radnim satima.", "Upozorenje", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        int prethodnaGodina = selectedSource.Godina;
        int prethodniMesec = selectedSource.Mesec;

        try
        {
            // Potraži sate za selektovani period u bazi, joinovane sa Radnik da dobijemo BrojRadnika
            var prethodniSatiList = await _db.RadniSati
                .Where(s => s.Godina == prethodnaGodina && s.Mesec == prethodniMesec)
                .Include(s => s.Radnik)
                .ToListAsync();

            var prethodniSatiByBrojRadnika = prethodniSatiList
                .Where(s => s.Radnik != null)
                .ToDictionary(s => s.Radnik.BrojRadnika);

            if (prethodniSatiByBrojRadnika.Count == 0)
            {
                MessageBox.Show(
                    $"Nisu pronađeni sačuvani radni sati za izabrani mesec ({prethodniMesec}.{prethodnaGodina}) iz kojeg bi se preneli podaci.",
                    "Obaveštenje",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            int godina = (int)ComboGodina.SelectedItem;
            int mesec = (int)ComboMesec.SelectedItem;

            // Kopiraj aktivne radnike iz prethodnog meseca u novi ako još ne postoje
            KopirajRadnikeIzPerioda(prethodnaGodina, prethodniMesec, godina, mesec);

            // Ponovo učitaj radnike za tekući period u listu i UI grid
            LoadAktivneRadnike();

            decimal.TryParse(TxtVrednostBoda.Text, out decimal vrednostBoda);
            if (vrednostBoda <= 0) vrednostBoda = 1860.34m;
            int.TryParse(TxtFondCasova.Text, out int fondSati);
            if (fondSati <= 0) fondSati = 176;

            // Prenesi sve period-specifične parametre (Poreze, Doprinose, Banke, Samodoprinose) iz baze
            await PrenesiParametreIzIzvoraAsync(prethodnaGodina, prethodniMesec, godina, mesec, vrednostBoda, fondSati);

            int prenetoCount = 0;
            foreach (var r in _radniciSati)
            {
                if (prethodniSatiByBrojRadnika.TryGetValue(r.BrojRadnika, out var starSati))
                {
                    r.RedovniSati = starSati.RedovniSati;
                    r.BolovanjeSati = starSati.BolovanjeSati;
                    r.PrekovremeneSati = starSati.PrekovremeneSati;
                    r.GodisnjiOdmorSati = starSati.GodisnjiOdmorSati;
                    r.DrzavniPraznikSati = starSati.DrzavniPraznikSati;
                    r.NocniSati = starSati.NocniSati;
                    r.SmenskiSati = starSati.SmenskiSati;
                    r.RadPraznikomSati = starSati.RadPraznikomSati;
                    r.NocniRadPraznikomSati = starSati.NocniRadPraznikomSati;
                    r.PlacenoOdsustvoSati = starSati.PlacenoOdsustvoSati;
                    r.Stimulacija = starSati.Stimulacija;
                    r.RadNedeljomSati = starSati.RadNedeljomSati;
                    r.PlacenoZakonskiSati = starSati.PlacenoZakonskiSati;
                    r.BolovanjePreko60Sati = starSati.BolovanjePreko60Sati;
                    r.PorodiljskoOdsustvoSati = starSati.PorodiljskoOdsustvoSati;
                    r.Bolovanje100Sati = starSati.Bolovanje100Sati;
                    r.TopliObrokDani = starSati.TopliObrokDani;
                    r.RegresIznos = starSati.RegresIznos;
                    r.Varijabila = starSati.Varijabila;
                    // Prosek se izračunava za NOVI (ciljni) period — ne prenosi se iz starog
                    r.Prosek = _obracunService.IzracunajProsekRadnika(r.RadnikId, godina, mesec);
                    prenetoCount++;
                }
            }

            GridRadniciSati.Items.Refresh();
            TxtObavestenje.Text = $"📋 Uspešno preneti svi podaci (sati, porezi, doprinosi, banke) iz perioda {prethodniMesec}.{prethodnaGodina} za {prenetoCount} radnika.";
            MessageBox.Show(
                $"Uspešno preneti svi podaci (sati, porezi, doprinosi, banke) iz perioda {prethodniMesec}.{prethodnaGodina} za {prenetoCount} radnika.",
                "Uspeh",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Greška prilikom prenosa podataka: {ex.Message}", "Greška", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void LnkPorezi_Click(object sender, RoutedEventArgs e)
    {
        var mainWin = Application.Current.MainWindow as MainWindow;
        if (mainWin != null)
        {
            mainWin.OtvoriPorezi();
        }
        Close();
    }

    private async Task PrenesiParametreIzIzvoraAsync(int sourceGodina, int sourceMesec, int targetGodina, int targetMesec, decimal targetVrBoda, int targetFondCasova)
    {
        // 1. Prenos Poreza
        var targetPorezi = await _db.Porezi.FirstOrDefaultAsync(p => p.Godina == targetGodina && p.Mesec == targetMesec);
        if (targetPorezi == null)
        {
            var sourcePorezi = await _db.Porezi.FirstOrDefaultAsync(p => p.Godina == sourceGodina && p.Mesec == sourceMesec);
            if (sourcePorezi != null)
            {
                var newPorezi = new PlataData.Models.Porezi
                {
                    Godina = targetGodina,
                    Mesec = targetMesec,
                    VrBoda = targetVrBoda,
                    FondCasova = targetFondCasova,
                    Zarada = sourcePorezi.Zarada,
                    AkPorez = sourcePorezi.AkPorez,
                    AkPorez2 = sourcePorezi.AkPorez2,
                    AkPorez3 = sourcePorezi.AkPorez3,
                    AkPorez4 = sourcePorezi.AkPorez4,
                    Prvast = sourcePorezi.Prvast,
                    Drugast = sourcePorezi.Drugast,
                    Trecast = sourcePorezi.Trecast,
                    LinPorez3 = sourcePorezi.LinPorez3,
                    SifPlac1 = sourcePorezi.SifPlac1,
                    ZiroR1 = sourcePorezi.ZiroR1,
                    PozivNa1 = sourcePorezi.PozivNa1,
                    PozivNa3 = sourcePorezi.PozivNa3,
                    Svrha1 = sourcePorezi.Svrha1,
                    Svrha2 = sourcePorezi.Svrha2,
                    Primalac1 = sourcePorezi.Primalac1,
                    Primalac2 = sourcePorezi.Primalac2,
                    SifPlac2 = sourcePorezi.SifPlac2,
                    ZiroR2 = sourcePorezi.ZiroR2,
                    PozivNa2 = sourcePorezi.PozivNa2,
                    PozivNa4 = sourcePorezi.PozivNa4,
                    PosPorez = sourcePorezi.PosPorez,
                    Svrha3 = sourcePorezi.Svrha3,
                    Svrha4 = sourcePorezi.Svrha4,
                    Primalac3 = sourcePorezi.Primalac3,
                    Primalac4 = sourcePorezi.Primalac4,
                    ProcDrzav = sourcePorezi.ProcDrzav,
                    ProcNocni = sourcePorezi.ProcNocni,
                    ProcPreko = sourcePorezi.ProcPreko,
                    ProcMinul = sourcePorezi.ProcMinul,
                    ProcNedel = sourcePorezi.ProcNedel,
                    ProcBolov = sourcePorezi.ProcBolov,
                    ProcPlac = sourcePorezi.ProcPlac,
                    ProcPlZa = sourcePorezi.ProcPlZa,
                    ProcInval = sourcePorezi.ProcInval,
                    CasZaOb = sourcePorezi.CasZaOb,
                    ProcIzdrz = sourcePorezi.ProcIzdrz,
                    Akont = sourcePorezi.Akont,
                    ProsBrut = sourcePorezi.ProsBrut,
                    TopliObrokCena = sourcePorezi.TopliObrokCena
                };
                _db.Porezi.Add(newPorezi);
            }
        }
        else
        {
            targetPorezi.VrBoda = targetVrBoda;
            targetPorezi.FondCasova = targetFondCasova;
            _db.Entry(targetPorezi).State = EntityState.Modified;
        }

        // 2. Prenos Doprinosa
        var imaTargetDoprinosi = await _db.Doprinosi.AnyAsync(d => d.Godina == targetGodina && d.Mesec == targetMesec);
        if (!imaTargetDoprinosi)
        {
            var sourceDoprinosi = await _db.Doprinosi.Where(d => d.Godina == sourceGodina && d.Mesec == sourceMesec).ToListAsync();
            foreach (var sd in sourceDoprinosi)
            {
                var newDop = new Doprinos
                {
                    Godina = targetGodina,
                    Mesec = targetMesec,
                    RedniBroj = sd.RedniBroj,
                    Naziv = sd.Naziv,
                    ProcRadn = sd.ProcRadn,
                    ProcPosl = sd.ProcPosl,
                    B60ProcR = sd.B60ProcR,
                    B60ProcP = sd.B60ProcP,
                    Bp60ProcP = sd.Bp60ProcP,
                    Bp60FProcP = sd.Bp60FProcP,
                    PorProcP = sd.PorProcP,
                    NepProcP = sd.NepProcP,
                    InvProcP = sd.InvProcP,
                    Svrha1 = sd.Svrha1,
                    Svrha2 = sd.Svrha2,
                    Primalac1 = sd.Primalac1,
                    Primalac2 = sd.Primalac2,
                    ZiroRacun = sd.ZiroRacun,
                    ZiroRacP = sd.ZiroRacP,
                    PozivNaB = sd.PozivNaB,
                    PozivNa2 = sd.PozivNa2,
                    SifPlac = sd.SifPlac,
                    SifPlacP = sd.SifPlacP
                };
                _db.Doprinosi.Add(newDop);
            }
        }

        // 3. Prenos Banaka
        var imaTargetBanke = await _db.Banke.AnyAsync(b => b.Godina == targetGodina && b.Mesec == targetMesec);
        if (!imaTargetBanke)
        {
            var sourceBanke = await _db.Banke.Where(b => b.Godina == sourceGodina && b.Mesec == sourceMesec).ToListAsync();
            foreach (var sb in sourceBanke)
            {
                var newBank = new Banka
                {
                    Godina = targetGodina,
                    Mesec = targetMesec,
                    Sifra = sb.Sifra,
                    Naziv = sb.Naziv,
                    ZiroRacun = sb.ZiroRacun
                };
                _db.Banke.Add(newBank);
            }
        }

        // 4. Prenos Samodoprinosa
        var imaTargetSamo = await _db.Samodoprinosi.AnyAsync(s => s.Godina == targetGodina && s.Mesec == targetMesec);
        if (!imaTargetSamo)
        {
            var sourceSamo = await _db.Samodoprinosi
                .Where(s => s.Godina == sourceGodina && s.Mesec == sourceMesec)
                .Include(s => s.Radnik)
                .ToListAsync();

            var targetRadnici = await _db.Radnici
                .Where(r => r.Godina == targetGodina && r.Mesec == targetMesec)
                .ToDictionaryAsync(r => r.BrojRadnika, r => r.Id);

            foreach (var ss in sourceSamo)
            {
                if (ss.Radnik != null && targetRadnici.TryGetValue(ss.Radnik.BrojRadnika, out var targetRadnikId))
                {
                    var newSamo = new Samodoprinosi
                    {
                        RadnikId = targetRadnikId,
                        Godina = targetGodina,
                        Mesec = targetMesec,
                        Iznos = ss.Iznos,
                        Opis = ss.Opis
                    };
                    _db.Samodoprinosi.Add(newSamo);
                }
            }
        }

        await _db.SaveChangesAsync();
    }

    private void KopirajRadnikeIzPerioda(int sourceGodina, int sourceMesec, int targetGodina, int targetMesec)
    {
        var imaTarget = _db.Radnici.Any(r => r.Godina == targetGodina && r.Mesec == targetMesec);
        if (imaTarget) return;

        var sourceRadnici = _db.Radnici
            .Where(r => r.Godina == sourceGodina && r.Mesec == sourceMesec && r.Aktivan)
            .ToList();

        foreach (var sr in sourceRadnici)
        {
            var newRadnik = new Radnik
            {
                Godina = targetGodina,
                Mesec = targetMesec,
                BrojRadnika = sr.BrojRadnika,
                ImeIPrezime = sr.ImeIPrezime,
                Jmbg = sr.Jmbg,
                MaticniBroj = sr.MaticniBroj,
                DatumRodjenja = sr.DatumRodjenja,
                MestoRodjenja = sr.MestoRodjenja,
                AdresaStanovanja = sr.AdresaStanovanja,
                Mesto = sr.Mesto,
                SifraOpstine = sr.SifraOpstine,
                DatumZaposlenja = sr.DatumZaposlenja,
                DatumPrestanka = sr.DatumPrestanka,
                Kategorija = sr.Kategorija,
                Radno_Mesto = sr.Radno_Mesto,
                BrojRadneJedinice = sr.BrojRadneJedinice,
                MinuliRadGodine = sr.MinuliRadGodine,
                Koeficijent = sr.Koeficijent,
                Koeficijent1 = sr.Koeficijent1,
                OsnovnaPlata = sr.OsnovnaPlata,
                StopaPio = sr.StopaPio,
                StopaZdravstvo = sr.StopaZdravstvo,
                StopaNezaposlenost = sr.StopaNezaposlenost,
                BankovniRacun = sr.BankovniRacun,
                NazivBanke = sr.NazivBanke,
                Aktivan = sr.Aktivan,
                LicnoOslobodjenje = sr.LicnoOslobodjenje,
                Operativni = sr.Operativni
            };
            _db.Radnici.Add(newRadnik);
        }
        _db.SaveChanges();
    }

    private async Task OsigurajParametreZaCiljniMesecAsync(int targetGodina, int targetMesec, decimal targetVrBoda, int targetFondCasova)
    {
        var targetPorezi = await _db.Porezi.FirstOrDefaultAsync(p => p.Godina == targetGodina && p.Mesec == targetMesec);
        if (targetPorezi != null)
        {
            targetPorezi.VrBoda = targetVrBoda;
            targetPorezi.FondCasova = targetFondCasova;
            _db.Entry(targetPorezi).State = EntityState.Modified;
            await _db.SaveChangesAsync();
            return;
        }

        var sourcePorezi = await _db.Porezi
            .Where(p => p.Godina < targetGodina || (p.Godina == targetGodina && p.Mesec < targetMesec))
            .OrderByDescending(p => p.Godina)
            .ThenByDescending(p => p.Mesec)
            .FirstOrDefaultAsync();

        if (sourcePorezi != null)
        {
            await PrenesiParametreIzIzvoraAsync(sourcePorezi.Godina, sourcePorezi.Mesec, targetGodina, targetMesec, targetVrBoda, targetFondCasova);
        }
    }
}

public class PrenosPeriodItem
{
    public int Godina { get; set; }
    public int Mesec { get; set; }
    
    public string Naziv
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
            return $"{Mesec:D2}/{Godina}";
        }
    }
    
    public override string ToString() => Naziv;
}

public class RadnikSatiInput
{
    public int RadnikId { get; set; }
    public int BrojRadnika { get; set; }
    public string ImeIPrezime { get; set; } = "";
    public decimal Koeficijent { get; set; }
    public int RedovniSati { get; set; }
    public int BolovanjeSati { get; set; }
    public int PrekovremeneSati { get; set; }
    public int GodisnjiOdmorSati { get; set; }
    public int DrzavniPraznikSati { get; set; }
    public int NocniSati { get; set; }
    public int SmenskiSati { get; set; }
    public int RadPraznikomSati { get; set; }
    public int NocniRadPraznikomSati { get; set; }
    public int PlacenoOdsustvoSati { get; set; }
    public decimal Stimulacija { get; set; }
    public int RadNedeljomSati { get; set; }
    public int PlacenoZakonskiSati { get; set; }
    public int BolovanjePreko60Sati { get; set; }
    public int PorodiljskoOdsustvoSati { get; set; }
    public int Bolovanje100Sati { get; set; }
    public int TopliObrokDani { get; set; }
    public decimal RegresIznos { get; set; }
    public decimal Varijabila { get; set; }
    public decimal Prosek { get; set; }
}
