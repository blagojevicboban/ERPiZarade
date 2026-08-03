using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.EntityFrameworkCore;
using ERPiZaradeData;
using ERPiZaradeData.Models;
using ERPiZaradeApp.Services;

namespace ERPiZaradeApp.Views.Radnici;

public class RadniciViewModel : INotifyPropertyChanged
{
    private readonly PlataDbContext _db;
    private ObservableCollection<Radnik> _radnici = [];
    private Radnik? _selectedRadnik;
    private Radnik? _editingRadnik;
    private string _searchText = "";
    private bool _prikazujeSamoAktivne = true;
    private bool _isEditing;
    private string _statusPoruka = "";
    private List<Banka> _availableBanke = [];
    private List<string> _availableMesta = [];
    private List<OpstinaInfo> _availableOpstine = [];

    public List<string> AvailableMesta
    {
        get => _availableMesta;
        set { _availableMesta = value; OnPropertyChanged(); }
    }

    public List<OpstinaInfo> AvailableOpstine
    {
        get => _availableOpstine;
        set { _availableOpstine = value; OnPropertyChanged(); }
    }

    // ── Polja za ŠVP padajuće liste ──────────────────────────────────
    private string _selectedPP = "01";
    private string _selectedOL = "00";
    private string _selectedB = "0";
    private bool _daLiJeStandardniSvp = true;

    public List<Banka> AvailableBanke
    {
        get => _availableBanke;
        set { _availableBanke = value; OnPropertyChanged(); }
    }

    public RadniciViewModel()
    {
        _db = PlataDbContext.Create(AppConfig.DbPath);
        LoadCommand = new RelayCommand(async _ => await LoadAsync());
        NewCommand = new RelayCommand(_ => NewRadnik(), _ => !IsEditing);
        SaveCommand = new RelayCommand(async _ => await SaveAsync(), _ => IsEditing && EditingRadnik != null);
        CancelCommand = new RelayCommand(_ => CancelEdit(), _ => IsEditing);
        DeleteCommand = new RelayCommand(async _ => await DeleteAsync(), _ => SelectedRadnik != null && !IsEditing);

        AvailableMesta = DbfHelper.LoadMesta();
        AvailableOpstine = DbfHelper.LoadOpstine();

        _ = LoadAsync();
    }

    public ObservableCollection<Radnik> Radnici
    {
        get => _radnici;
        set { _radnici = value; OnPropertyChanged(); }
    }

    public Radnik? SelectedRadnik
    {
        get => _selectedRadnik;
        set
        {
            _selectedRadnik = value;
            OnPropertyChanged();
            if (!IsEditing && value != null)
            {
                EditingRadnik = CopyRadnik(value);
                AzurirajKombinovanaSvpPolja();
            }
        }
    }

    public Radnik? EditingRadnik
    {
        get => _editingRadnik;
        set { _editingRadnik = value; OnPropertyChanged(); }
    }

    public string SearchText
    {
        get => _searchText;
        set { _searchText = value; OnPropertyChanged(); _ = LoadAsync(); }
    }

    public bool PrikazujeSamoAktivne
    {
        get => _prikazujeSamoAktivne;
        set { _prikazujeSamoAktivne = value; OnPropertyChanged(); _ = LoadAsync(); }
    }

    public bool IsEditing
    {
        get => _isEditing;
        set { _isEditing = value; OnPropertyChanged(); }
    }

    public string StatusPoruka
    {
        get => _statusPoruka;
        set { _statusPoruka = value; OnPropertyChanged(); }
    }

    // ── Properties za ŠVP padajuće liste ──────────────────────────────
    public bool DaLiJeStandardniSvp
    {
        get => _daLiJeStandardniSvp;
        set
        {
            _daLiJeStandardniSvp = value;
            OnPropertyChanged();
            if (value)
            {
                RekombinujSvpKod();
            }
        }
    }

    public string SelectedPP
    {
        get => _selectedPP;
        set
        {
            _selectedPP = value;
            OnPropertyChanged();
            RekombinujSvpKod();
        }
    }

    public string SelectedOL
    {
        get => _selectedOL;
        set
        {
            _selectedOL = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ImaOlaksicu));
            RekombinujSvpKod();
        }
    }

    /// <summary>
    /// Da li je izabrana neka poreska olakšica („00" = bez olakšice). Upravlja prikazom
    /// procenata povraćaja — oni imaju smisla samo uz olakšicu.
    /// </summary>
    public bool ImaOlaksicu => !string.IsNullOrEmpty(_selectedOL) && _selectedOL != "00";

    public string SelectedB
    {
        get => _selectedB;
        set
        {
            _selectedB = value;
            OnPropertyChanged();
            RekombinujSvpKod();
        }
    }

    public List<SvpOption> OpcijePP { get; } = new()
    {
        new() { Sifra = "01", Naziv = "Redovan zaposleni" },
        new() { Sifra = "09", Naziv = "Zaposleni penzioner" },
        new() { Sifra = "03", Naziv = "Zaposleni vojni osiguranik" },
        new() { Sifra = "04", Naziv = "Zaposleni osiguranik MUP/BIA" },
        new() { Sifra = "21", Naziv = "Stručno osposobljavanje" }
    };

    /// <summary>
    /// Olakšice iz šifarnika. Ranije je ovo bila lista ugrađena u kod, sa oznakama koje su se
    /// menjale izmenama propisa — a oznaka ulazi u SVP šifru koja ide u PPP-PD, pa pogrešna
    /// oznaka znači pogrešnu prijavu. Sada se ispravlja u šifarniku, bez izmene koda.
    /// </summary>
    public List<SvpOption> OpcijeOL { get; } = UcitajOpcijeOL();

    private static List<SvpOption> UcitajOpcijeOL()
    {
        var opcije = new List<SvpOption> { new() { Sifra = "00", Naziv = "Bez poreskih olakšica" } };

        try
        {
            using var db = PlataDbContext.Create(AppConfig.DbPath);
            opcije.AddRange(db.PoreskeOlaksice
                .AsNoTracking()
                .Where(o => o.Aktivna)
                .OrderBy(o => o.Sifra)
                .ToList()
                .Select(o => new SvpOption
                {
                    Sifra = o.Sifra,
                    Naziv = string.IsNullOrWhiteSpace(o.PravniOsnov) ? o.Naziv : $"{o.Naziv} ({o.PravniOsnov})"
                }));
        }
        catch
        {
            // Baza starije verzije nema šifarnik — ostaje samo „bez olakšica", pa se karton
            // može uređivati i dok se šifarnik ne popuni.
        }

        return opcije;
    }

    public List<SvpOption> OpcijeB { get; } = new()
    {
        new() { Sifra = "0", Naziv = "Nema beneficiranog staža" },
        new() { Sifra = "1", Naziv = "Stepen uvećanja 12/14 (cifra 1)" },
        new() { Sifra = "2", Naziv = "Stepen uvećanja 12/16 (cifra 2)" },
        new() { Sifra = "3", Naziv = "Stepen uvećanja 12/18 (cifra 3)" },
        new() { Sifra = "4", Naziv = "Stepen uvećanja 12/15 (cifra 4)" }
    };

    public List<SvpOption> OpcijeStrucneSpreme { get; } = new()
    {
        new() { Sifra = "1", Naziv = "I/II - stepen - 4/8 razreda osnovne škole" },
        new() { Sifra = "2", Naziv = "II - stepen - osnovna škola" },
        new() { Sifra = "3", Naziv = "III - stepen - srednja škola" },
        new() { Sifra = "4", Naziv = "IV - stepen - srednja škola" },
        new() { Sifra = "5", Naziv = "V - stepen - VKV srednja škola" },
        new() { Sifra = "6", Naziv = "VI - stepen - viša škola" },
        new() { Sifra = "7", Naziv = "VII - stepen - visoka škola" },
        new() { Sifra = "72", Naziv = "VII-2 - stepen - specijalizacija/magistratura" },
        new() { Sifra = "8", Naziv = "VIII - stepen - doktorat" }
    };

    private void AzurirajKombinovanaSvpPolja()
    {
        if (EditingRadnik == null) return;
        var rMesto = EditingRadnik.Radno_Mesto ?? "";

        // Standardna šifra za redovnu isplatu: počinje sa 1, dužine 9, na pozicijama 3-5 stoji 101 (OVP za zaradu)
        if (rMesto.Length == 9 && rMesto.All(char.IsDigit) && rMesto.StartsWith("1") && rMesto.Substring(3, 3) == "101")
        {
            _daLiJeStandardniSvp = true;
            OnPropertyChanged(nameof(DaLiJeStandardniSvp));

            string pp = rMesto.Substring(1, 2);
            string ol = rMesto.Substring(6, 2);
            string b = rMesto.Substring(8, 1);

            _selectedPP = pp;
            _selectedOL = ol;
            _selectedB = b;

            OnPropertyChanged(nameof(SelectedPP));
            OnPropertyChanged(nameof(SelectedOL));
            OnPropertyChanged(nameof(SelectedB));
            OnPropertyChanged(nameof(ImaOlaksicu));
        }
        else
        {
            _daLiJeStandardniSvp = false;
            OnPropertyChanged(nameof(DaLiJeStandardniSvp));
        }
    }

    private void RekombinujSvpKod()
    {
        if (EditingRadnik == null || !DaLiJeStandardniSvp) return;
        EditingRadnik.Radno_Mesto = $"1{SelectedPP}101{SelectedOL}{SelectedB}";
        OnPropertyChanged(nameof(EditingRadnik));
    }

    // ── Komande i akcije ──────────────────────────────────────────────
    public ICommand LoadCommand { get; }
    public ICommand NewCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand DeleteCommand { get; }

    private async Task LoadAsync()
    {
        try
        {
            int godina = AppConfig.ActiveGodina ?? DateTime.Now.Year;
            int mesec = AppConfig.ActiveMesec ?? DateTime.Now.Month;

            // Automatska ispravka: Deaktiviraj sve '[Bivši zaposleni]' zapise u svim periodima
            var bivsiAktivni = await _db.Radnici
                .Where(r => r.ImeIPrezime.Contains("Bivši zaposleni") && r.Aktivan)
                .ToListAsync();
            if (bivsiAktivni.Count > 0)
            {
                foreach (var b in bivsiAktivni)
                {
                    b.Aktivan = false;
                }
                await _db.SaveChangesAsync();
            }

            // Ako je '[Bivši zaposleni]' greškom kopiran u tekući period a nema sati ni obračuna, obriši ga
            var bivsiTekuci = await _db.Radnici
                .Where(r => r.Godina == godina && r.Mesec == mesec && r.ImeIPrezime.Contains("Bivši zaposleni"))
                .ToListAsync();
            if (bivsiTekuci.Count > 0)
            {
                var toDelete = new List<Radnik>();
                foreach (var bt in bivsiTekuci)
                {
                    var imaSate = await _db.RadniSati.AnyAsync(s => s.RadnikId == bt.Id && (s.RedovniSati > 0 || s.BolovanjeSati > 0 || s.PrekovremeneSati > 0 || s.GodisnjiOdmorSati > 0 || s.DrzavniPraznikSati > 0 || s.NocniSati > 0 || s.Stimulacija > 0));
                    var imaObracun = await _db.ObracuniPlata.AnyAsync(o => o.RadnikId == bt.Id);
                    if (!imaSate && !imaObracun)
                    {
                        toDelete.Add(bt);
                    }
                }
                if (toDelete.Count > 0)
                {
                    await SafeDeleteWorkersAsync(_db, toDelete);
                    await _db.SaveChangesAsync();
                }
            }

            // Ako uopšte nema aktivnih radnika u tekućem periodu, a nema ni obračuna zarada u ovom mesecu
            var imaAktivnihRadnika = await _db.Radnici.AnyAsync(r => r.Godina == godina && r.Mesec == mesec && r.Aktivan);
            var imaObracunaUMesecu = await _db.ObracuniPlata.AnyAsync(o => o.Godina == godina && o.Mesec == mesec);
            if (!imaAktivnihRadnika && !imaObracunaUMesecu)
            {
                // Izbriši sve neaktivne iz tekućeg meseca ako nema obračuna, da bismo uradili čist re-import
                var neaktivniTekuci = await _db.Radnici.Where(r => r.Godina == godina && r.Mesec == mesec).ToListAsync();
                if (neaktivniTekuci.Count > 0)
                {
                    await SafeDeleteWorkersAsync(_db, neaktivniTekuci);
                    await _db.SaveChangesAsync();
                }

                // Nađi najbliži prethodni period koji ima aktivne radnike (koji je strogo pre tekućeg)
                var sourcePeriod = await _db.Radnici
                    .Where(r => r.Godina < godina || (r.Godina == godina && r.Mesec < mesec))
                    .OrderByDescending(r => r.Godina)
                    .ThenByDescending(r => r.Mesec)
                    .Select(r => new { r.Godina, r.Mesec })
                    .FirstOrDefaultAsync();

                if (sourcePeriod != null)
                {
                    var sourceRadnici = await _db.Radnici
                        .Where(r => r.Godina == sourcePeriod.Godina && r.Mesec == sourcePeriod.Mesec && r.Aktivan)
                        .ToListAsync();

                    foreach (var sr in sourceRadnici)
                    {
                        var newRadnik = new Radnik
                        {
                            Godina = godina,
                            Mesec = mesec,
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
                            VanRadnogOdnosa = sr.VanRadnogOdnosa,
                            LicnoOslobodjenje = sr.LicnoOslobodjenje,
                            Operativni = sr.Operativni,
                            Email = sr.Email,
                            SifraMestaTroska = sr.SifraMestaTroska,
                            ProcenatPovracajaPoreza = sr.ProcenatPovracajaPoreza,
                            ProcenatPovracajaDoprinosa = sr.ProcenatPovracajaDoprinosa,
                            OlaksicaVaziDo = sr.OlaksicaVaziDo
                        };
                        _db.Radnici.Add(newRadnik);
                    }
                    await _db.SaveChangesAsync();
                }
            }

            var query = _db.Radnici.AsNoTracking().Where(r => r.Godina == godina && r.Mesec == mesec);
            if (PrikazujeSamoAktivne)
                query = query.Where(r => r.Aktivan);
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var lowerSearch = SearchText.ToLower();
                query = query.Where(r => r.ImeIPrezime.ToLower().Contains(lowerSearch) ||
                                         r.BrojRadnika.ToString().Contains(lowerSearch) ||
                                         r.Jmbg.Contains(lowerSearch) ||
                                         r.MaticniBroj.ToLower().Contains(lowerSearch));
            }

            var list = await query.OrderBy(r => r.BrojRadnika).ToListAsync();
            Radnici = new ObservableCollection<Radnik>(list);

            // Učitaj aktivne banke bez tracking-a
            var bankeList = await _db.Banke
                .AsNoTracking()
                .Where(b => b.Godina == godina && b.Mesec == mesec)
                .OrderBy(b => b.Naziv)
                .ToListAsync();

            if (bankeList.Count == 0)
            {
                var closest = await _db.Banke
                    .AsNoTracking()
                    .OrderByDescending(b => b.Godina)
                    .ThenByDescending(b => b.Mesec)
                    .FirstOrDefaultAsync();

                if (closest != null)
                {
                    bankeList = await _db.Banke
                        .AsNoTracking()
                        .Where(b => b.Godina == closest.Godina && b.Mesec == closest.Mesec)
                        .OrderBy(b => b.Naziv)
                        .ToListAsync();
                }
            }

            if (bankeList.Count == 0)
            {
                bankeList = new List<Banka>
                {
                    new Banka { Sifra = "1", Naziv = "Gotovina" },
                    new Banka { Sifra = "2", Naziv = "BANKA INTESA" }
                };
            }
            AvailableBanke = bankeList;

            StatusPoruka = $"Prikazano: {list.Count} radnika za period {mesec}.{godina}.";
        }
        catch (Exception ex)
        {
            StatusPoruka = $"Greška pri učitavanju: {ex.Message}";
        }
    }

    private void NewRadnik()
    {
        int godina = AppConfig.ActiveGodina ?? DateTime.Now.Year;
        int mesec = AppConfig.ActiveMesec ?? DateTime.Now.Month;
        int nextBroj = (_db.Radnici.Where(r => r.Godina == godina && r.Mesec == mesec).Select(r => (int?)r.BrojRadnika).Max() ?? 0) + 1;
        EditingRadnik = new Radnik { Aktivan = true, BrojRadneJedinice = 1, BrojRadnika = nextBroj, Radno_Mesto = "101101000", Godina = godina, Mesec = mesec };
        SelectedRadnik = null;
        IsEditing = true;
        StatusPoruka = "Unos novog radnika...";
        AzurirajKombinovanaSvpPolja();
    }

    private async Task SaveAsync()
    {
        if (EditingRadnik == null) return;
        
        int godina = AppConfig.ActiveGodina ?? DateTime.Now.Year;
        int mesec = AppConfig.ActiveMesec ?? DateTime.Now.Month;

        // Osiguraj da EditingRadnik ima postavljen tekući period pre provera i čuvanja
        EditingRadnik.Godina = godina;
        EditingRadnik.Mesec = mesec;

        bool isLocked = await _db.ObracuniPlata.AnyAsync(o => o.Godina == godina && o.Mesec == mesec && o.Zakljucan);
        if (isLocked)
        {
            System.Windows.MessageBox.Show("Obračunski period je ZAKLJUČAN. Izmene podataka o radnicima u ovom periodu nisu dozvoljene.", "Upozorenje", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            return;
        }

        try
        {
            // Validacija JMBG-a (ako je uključena u podešavanjima)
            if (UserSettings.Instance.ValidacijaJmbgOmogucena && !JmbgValidator.Validate(EditingRadnik.Jmbg, out string jmbgError))
            {
                System.Windows.MessageBox.Show(
                    $"Podaci o zaposlenom ne mogu biti sačuvani jer JMBG nije ispravan:\n\n• {jmbgError}\n\nMolimo vas da unesete validan JMBG ili isključite ovu proveru u Podešavanjima.",
                    "Neispravan JMBG",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
                StatusPoruka = $"Greška pri čuvanju: {jmbgError}";
                return;
            }

            // Provera jedinstvenosti broja radnika u tekućem periodu (BrojRadnika, Godina, Mesec)
            var postojeciSaIstimBrojem = await _db.Radnici
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Godina == godina && r.Mesec == mesec &&
                                         r.BrojRadnika == EditingRadnik.BrojRadnika &&
                                         r.Id != EditingRadnik.Id);

            if (postojeciSaIstimBrojem != null)
            {
                System.Windows.MessageBox.Show(
                    $"Broj radnika '{EditingRadnik.BrojRadnika}' je već zauzet u obračunskom periodu {mesec:D2}/{godina}.\n(Zaposleni: {postojeciSaIstimBrojem.ImeIPrezime})\n\nMolimo vas da unesete drugi broj radnika.",
                    "Dupliran broj radnika",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
                StatusPoruka = $"Greška: Broj radnika {EditingRadnik.BrojRadnika} je već zauzet ({postojeciSaIstimBrojem.ImeIPrezime}).";
                return;
            }

            if (EditingRadnik.Id == 0)
            {
                EditingRadnik.DatumUnosa = DateTime.Now;
                if (EditingRadnik.BrojRadnika == 0)
                {
                    EditingRadnik.BrojRadnika = (_db.Radnici.Where(r => r.Godina == godina && r.Mesec == mesec).Select(r => (int?)r.BrojRadnika).Max() ?? 0) + 1;
                }

                _db.Radnici.Add(EditingRadnik);
                await _db.SaveChangesAsync();
                StatusPoruka = $"Radnik {EditingRadnik.ImeIPrezime} dodat.";
            }
            else
            {
                var existing = await _db.Radnici.FindAsync(EditingRadnik.Id);
                if (existing != null)
                {
                    EditingRadnik.DatumIzmene = DateTime.Now;
                    _db.Entry(existing).CurrentValues.SetValues(EditingRadnik);
                    await _db.SaveChangesAsync();
                    StatusPoruka = $"Radnik {EditingRadnik.ImeIPrezime} sačuvan.";
                }
            }
            IsEditing = false;
            await LoadAsync();
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException dbEx) when (dbEx.InnerException?.Message.Contains("UNIQUE constraint failed") == true || dbEx.Message.Contains("UNIQUE constraint failed"))
        {
            System.Windows.MessageBox.Show(
                $"Broj radnika '{EditingRadnik.BrojRadnika}' je već zauzet u ovom obračunskom periodu ({mesec:D2}/{godina}).\n\nMolimo vas da unesete drugi broj radnika.",
                "Dupliran broj radnika",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
            StatusPoruka = $"Greška pri čuvanju: Broj radnika {EditingRadnik.BrojRadnika} već postoji u periodu {mesec:D2}/{godina}.";
        }
        catch (Exception ex)
        {
            var msg = ex.Message;
            if (ex.InnerException != null)
            {
                msg += $" -> {ex.InnerException.Message}";
            }
            System.Windows.MessageBox.Show(
                $"Došlo je do greške pri čuvanju radnika:\n\n{msg}",
                "Greška pri čuvanju",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
            StatusPoruka = $"Greška pri čuvanju: {msg}";
        }
    }

    private void CancelEdit()
    {
        IsEditing = false;
        EditingRadnik = SelectedRadnik != null ? CopyRadnik(SelectedRadnik) : null;
        AzurirajKombinovanaSvpPolja();
        StatusPoruka = "Izmena otkazana.";
    }

    private async Task DeleteAsync()
    {
        if (SelectedRadnik == null) return;
        
        int godina = AppConfig.ActiveGodina ?? DateTime.Now.Year;
        int mesec = AppConfig.ActiveMesec ?? DateTime.Now.Month;
        bool isLocked = await _db.ObracuniPlata.AnyAsync(o => o.Godina == godina && o.Mesec == mesec && o.Zakljucan);
        if (isLocked)
        {
            System.Windows.MessageBox.Show("Obračunski period je ZAKLJUČAN. Brisanje (deaktivacija) radnika u ovom periodu nije dozvoljeno.", "Upozorenje", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            return;
        }

        var result = System.Windows.MessageBox.Show(
            $"Deaktivirate radnika {SelectedRadnik.ImeIPrezime}?",
            "Potvrda", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning);
        if (result != System.Windows.MessageBoxResult.Yes) return;

        try
        {
            var r = await _db.Radnici.FindAsync(SelectedRadnik.Id);
            if (r != null) { r.Aktivan = false; await _db.SaveChangesAsync(); }
            StatusPoruka = $"Radnik {SelectedRadnik.ImeIPrezime} deaktiviran.";
            await LoadAsync();
        }
        catch (Exception ex) { StatusPoruka = $"Greška: {ex.Message}"; }
    }

    private static async Task SafeDeleteWorkersAsync(PlataDbContext db, List<Radnik> workersToDelete)
    {
        if (workersToDelete == null || workersToDelete.Count == 0) return;
        var ids = workersToDelete.Select(w => w.Id).ToList();

        var dpRows = await db.DoprinosiPoslodavca.Where(dp => ids.Contains(dp.RadnikId)).ToListAsync();
        if (dpRows.Count > 0) db.DoprinosiPoslodavca.RemoveRange(dpRows);

        var rsRows = await db.RadniSati.Where(rs => ids.Contains(rs.RadnikId)).ToListAsync();
        if (rsRows.Count > 0) db.RadniSati.RemoveRange(rsRows);

        var opRows = await db.ObracuniPlata.Where(op => ids.Contains(op.RadnikId)).ToListAsync();
        if (opRows.Count > 0) db.ObracuniPlata.RemoveRange(opRows);

        var kRows = await db.Krediti.Where(k => ids.Contains(k.RadnikId)).ToListAsync();
        if (kRows.Count > 0) db.Krediti.RemoveRange(kRows);

        var sdRows = await db.Samodoprinosi.Where(sd => ids.Contains(sd.RadnikId)).ToListAsync();
        if (sdRows.Count > 0) db.Samodoprinosi.RemoveRange(sdRows);

        db.Radnici.RemoveRange(workersToDelete);
    }

    private static Radnik CopyRadnik(Radnik src) => new()
    {
        Id = src.Id,
        Godina = src.Godina,
        Mesec = src.Mesec,
        ImeIPrezime = src.ImeIPrezime,
        Jmbg = src.Jmbg,
        BrojRadnika = src.BrojRadnika,
        MaticniBroj = src.MaticniBroj,
        DatumRodjenja = src.DatumRodjenja,
        MestoRodjenja = src.MestoRodjenja,
        AdresaStanovanja = src.AdresaStanovanja,
        Mesto = src.Mesto,
        SifraOpstine = src.SifraOpstine,
        DatumZaposlenja = src.DatumZaposlenja,
        DatumPrestanka = src.DatumPrestanka,
        Kategorija = src.Kategorija,
        Radno_Mesto = src.Radno_Mesto,
        BrojRadneJedinice = src.BrojRadneJedinice,
        MinuliRadGodine = src.MinuliRadGodine,
        Koeficijent = src.Koeficijent,
        Koeficijent1 = src.Koeficijent1,
        OsnovnaPlata = src.OsnovnaPlata,
        StopaPio = src.StopaPio,
        StopaZdravstvo = src.StopaZdravstvo,
        StopaNezaposlenost = src.StopaNezaposlenost,
        BankovniRacun = src.BankovniRacun,
        NazivBanke = src.NazivBanke,
        Aktivan = src.Aktivan,
        VanRadnogOdnosa = src.VanRadnogOdnosa,
        LicnoOslobodjenje = src.LicnoOslobodjenje,
        Operativni = src.Operativni,
        Email = src.Email,
        SifraMestaTroska = src.SifraMestaTroska,
        ProcenatPovracajaPoreza = src.ProcenatPovracajaPoreza,
        ProcenatPovracajaDoprinosa = src.ProcenatPovracajaDoprinosa,
        OlaksicaVaziDo = src.OlaksicaVaziDo,
        DatumUnosa = src.DatumUnosa,
        DatumIzmene = src.DatumIzmene
    };

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>
/// Pomoćni model za stavke u padajućim listama.
/// </summary>
public class SvpOption
{
    public string Sifra { get; set; } = "";
    public string Naziv { get; set; } = "";
    public string Prikaz => $"{Sifra} - {Naziv}";
}

// ── Relay Command ─────────────────────────────────────────────────────────
public class RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null) : ICommand
{
    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }
    public bool CanExecute(object? p) => canExecute?.Invoke(p) ?? true;
    public void Execute(object? p) => execute(p);
}
