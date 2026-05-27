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

            if (!int.TryParse(TxtFondCasova.Text, out int fondSati))
                fondSati = 176;

            var aktivniRadnici = _db.Radnici
                .Where(r => r.Aktivan)
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
                    .Where(r => !r.Aktivan && radnikIdsSaSacuvanim.Contains(r.Id))
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
                        Prosek = sacuvaniSati.Prosek > 0 ? sacuvaniSati.Prosek : _obracunService.IzracunajProsekRadnika(r.Id, godina, mesec)
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

    private void BtnPrenesi_Click(object sender, RoutedEventArgs e)
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
            // Potraži sate za selektovani period u bazi
            var prethodniSati = _db.RadniSati
                .Where(s => s.Godina == prethodnaGodina && s.Mesec == prethodniMesec)
                .ToDictionary(s => s.RadnikId);

            if (prethodniSati.Count == 0)
            {
                MessageBox.Show(
                    $"Nisu pronađeni sačuvani radni sati za izabrani mesec ({prethodniMesec}.{prethodnaGodina}) iz kojeg bi se preneli podaci.",
                    "Obaveštenje",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            int prenetoCount = 0;
            foreach (var r in _radniciSati)
            {
                if (prethodniSati.TryGetValue(r.RadnikId, out var starSati))
                {
                    r.RedovniSati = starSati.RedovniSati;
                    r.BolovanjeSati = starSati.BolovanjeSati;
                    r.PrekovremeneSati = starSati.PrekovremeneSati;
                    r.GodisnjiOdmorSati = starSati.GodisnjiOdmorSati;
                    r.DrzavniPraznikSati = starSati.DrzavniPraznikSati;
                    r.NocniSati = starSati.NocniSati;
                    prenetoCount++;
                }
            }

            GridRadniciSati.Items.Refresh();
            TxtObavestenje.Text = $"📋 Uspešno preneti podaci o satima iz perioda {prethodniMesec}.{prethodnaGodina} za {prenetoCount} radnika.";
            MessageBox.Show(
                $"Uspešno preneti podaci o satima iz perioda {prethodniMesec}.{prethodnaGodina} za {prenetoCount} radnika.",
                "Uspeh",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Greška prilikom prenosa podataka: {ex.Message}", "Greška", MessageBoxButton.OK, MessageBoxImage.Error);
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
    public decimal Prosek { get; set; }
}
