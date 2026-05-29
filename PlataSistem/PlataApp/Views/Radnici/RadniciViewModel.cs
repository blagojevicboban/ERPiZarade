using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.EntityFrameworkCore;
using PlataData;
using PlataData.Models;
using PlataApp.Services;

namespace PlataApp.Views.Radnici;

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
            RekombinujSvpKod();
        }
    }

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

    public List<SvpOption> OpcijeOL { get; } = new()
    {
        new() { Sifra = "00", Naziv = "Bez poreskih olakšica" },
        new() { Sifra = "01", Naziv = "Novozaposleni 65% (čl. 21v st.1)" },
        new() { Sifra = "02", Naziv = "Novozaposleni 70% (čl. 21v st.2)" },
        new() { Sifra = "03", Naziv = "Novozaposleni 75% (čl. 21v st.3)" },
        new() { Sifra = "24", Naziv = "Kvalifikovano novozaposleno lice (čl. 21j)" },
        new() { Sifra = "32", Naziv = "Osnivač inovativnog preduzeća" }
    };

    public List<SvpOption> OpcijeB { get; } = new()
    {
        new() { Sifra = "0", Naziv = "Nema beneficiranog staža" },
        new() { Sifra = "1", Naziv = "Stepen uvećanja 12/14 (cifra 1)" },
        new() { Sifra = "2", Naziv = "Stepen uvećanja 12/16 (cifra 2)" },
        new() { Sifra = "3", Naziv = "Stepen uvećanja 12/18 (cifra 3)" },
        new() { Sifra = "4", Naziv = "Stepen uvećanja 12/15 (cifra 4)" }
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

            // Ako uopšte nema radnika u tekućem periodu, prekopiraj ih iz najbližeg prethodnog
            var imaTargetRadnika = await _db.Radnici.AnyAsync(r => r.Godina == godina && r.Mesec == mesec);
            if (!imaTargetRadnika)
            {
                var sourcePeriod = await _db.Radnici
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
                            LicnoOslobodjenje = sr.LicnoOslobodjenje,
                            Operativni = sr.Operativni
                        };
                        _db.Radnici.Add(newRadnik);
                    }
                    await _db.SaveChangesAsync();
                }
            }

            var query = _db.Radnici.Where(r => r.Godina == godina && r.Mesec == mesec);
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

            // Učitaj aktivne banke
            var bankeList = await _db.Banke
                .Where(b => b.Godina == godina && b.Mesec == mesec)
                .OrderBy(b => b.Naziv)
                .ToListAsync();

            if (bankeList.Count == 0)
            {
                var closest = await _db.Banke
                    .OrderByDescending(b => b.Godina)
                    .ThenByDescending(b => b.Mesec)
                    .FirstOrDefaultAsync();

                if (closest != null)
                {
                    bankeList = await _db.Banke
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
        try
        {
            int godina = AppConfig.ActiveGodina ?? DateTime.Now.Year;
            int mesec = AppConfig.ActiveMesec ?? DateTime.Now.Month;

            if (EditingRadnik.Id == 0)
            {
                int nextId = (_db.Radnici.Select(r => (int?)r.Id).Max() ?? 0) + 1;
                EditingRadnik.Id = nextId;
                EditingRadnik.Godina = godina;
                EditingRadnik.Mesec = mesec;
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
                    _db.Entry(existing).CurrentValues.SetValues(EditingRadnik);
                    await _db.SaveChangesAsync();
                    StatusPoruka = $"Radnik {EditingRadnik.ImeIPrezime} sačuvan.";
                }
            }
            IsEditing = false;
            await LoadAsync();
        }
        catch (Exception ex)
        {
            var msg = ex.Message;
            if (ex.InnerException != null)
            {
                msg += $" -> {ex.InnerException.Message}";
            }
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

    private static Radnik CopyRadnik(Radnik src) => new()
    {
        Id = src.Id,
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
        Koeficijent = src.Koeficijent,
        OsnovnaPlata = src.OsnovnaPlata,
        StopaPio = src.StopaPio,
        StopaZdravstvo = src.StopaZdravstvo,
        StopaNezaposlenost = src.StopaNezaposlenost,
        BankovniRacun = src.BankovniRacun,
        NazivBanke = src.NazivBanke,
        Aktivan = src.Aktivan,
        LicnoOslobodjenje = src.LicnoOslobodjenje,
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
