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
using ERPiZaradeApp.Views.Radnici; // Za RelayCommand
using ERPiZaradeApp.Services;

namespace ERPiZaradeApp.Views.PppPd;

public class PppPdViewModel : INotifyPropertyChanged
{
    private readonly PlataDbContext _db;
    
    // Period selection
    private ObservableCollection<int> _godine = [];
    private ObservableCollection<int> _meseci = [];
    private int _selectedGodina;
    private int _selectedMesec;
    
    // Employee calculations list
    private ObservableCollection<ObracunPlate> _obracuni = [];

    // Isplate u mesecu (Faza 2.2) — prijava se podnosi za jednu isplatu, ne za period
    private readonly IsplataService _isplataService;
    private ObservableCollection<Isplata> _isplate = [];
    private Isplata? _izabranaIsplata;
    
    // Company / Payer info
    private string _pib = "";
    private string _maticniBroj = "";
    private string _naziv = "NAZIV FIRME";
    private string _telefon = "";
    private string _adresa = "";
    private string _email = "";
    private string _sediste = "";
    
    // Declaration settings
    private DateTime _datumPlacanja = DateTime.Now;
    private string _selectedVrstaPrijave = "1";
    private string _selectedOznakaZaKonacnu = "K";
    private string _selectedNajnizaOsnovica = "0";
    private string _selectedTipIsplatioca = "1";
    private string _klijentskaOznaka = "";
    private int _brojKalendarskihDana = DateTime.DaysInMonth(DateTime.Now.Year, DateTime.Now.Month);
    private int _brojStorniranih;

    // Izmenjena prijava (Faza 2.7) — pozicije 1.5–1.6a Obrasca PPP-PD
    private VrstaIzmenePrijave _vrstaIzmene = VrstaIzmenePrijave.Nema;
    private string _jipdKojiSeMenja = "";
    private string _brojResenja = "";
    private OsnovIzmenePrijave _osnovIzmene = OsnovIzmenePrijave.Nema;

    // Summaries (KPIs)
    private int _totalRadnika;
    private decimal _ukupnoBruto;
    private decimal _ukupnoPorez;
    private decimal _ukupnoDoprinosi;
    
    // Validation properties
    private string _statusText = "";
    private ObservableCollection<string> _validationAlerts = [];
    private bool _prikaziUpozorenja;
    private bool _podaciSuValidni;

    public PppPdViewModel()
    {
        _db = PlataDbContext.Create(AppConfig.DbPath);
        _isplataService = new IsplataService(_db);

        LoadCommand = new RelayCommand(async _ => await LoadObracuneAsync());
        ValidateCommand = new RelayCommand(_ => ValidateData());
        
        Meseci = new ObservableCollection<int>(Enumerable.Range(1, 12));
        SelectedMesec = AppConfig.ActiveMesec ?? DateTime.Now.Month;
        
        // Default klijentska oznaka
        KlijentskaOznaka = $"DECL-{DateTime.Now:ddMMyyyy}";

        UcitajFirmaPodatke();
        UcitajSacuvanePostavke();
        // Šifra opštine sedišta je od Faze 0 svojstvo firme, a ne aplikacije. Za agencije
        // koje vode više firmi jedna vrednost u postavkama daje pogrešno zaglavlje svima
        // osim jednoj, pa podatak iz kartona firme ima prednost nad zapamćenom postavkom.
        PreuzmiSedisteIzFirme();

        SaveCommand = new RelayCommand(_ => SacuvajPostavke());

        _ = InitAsync();
    }

    private void UcitajFirmaPodatke()
    {
        try
        {
            var firma = _db.Firme.FirstOrDefault();
            if (firma != null)
            {
                if (!string.IsNullOrWhiteSpace(firma.Pib)) Pib = firma.Pib;
                if (!string.IsNullOrWhiteSpace(firma.Mb)) MaticniBroj = firma.Mb;
                if (!string.IsNullOrWhiteSpace(firma.Naziv)) Naziv = firma.Naziv;
                if (!string.IsNullOrWhiteSpace(firma.Telefon)) Telefon = firma.Telefon;
                if (!string.IsNullOrWhiteSpace(firma.Adresa)) Adresa = $"{firma.Adresa} {firma.Grad}".Trim();
                if (!string.IsNullOrWhiteSpace(firma.Email)) Email = firma.Email;
            }
        }
        catch { }
    }

    /// <summary>
    /// MFP deklaracije po OL oznaci olakšice. Prijava ih uzima odavde, a ne iz koda, jer
    /// značenje MFP polja zavisi od SVP šifre i propisuje ga katalog Poreske uprave.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<OlaksicaMfp>> MfpPoOlaksici
    {
        get
        {
            try
            {
                return _db.PoreskeOlaksice
                    .AsNoTracking()
                    .Include(o => o.MfpDeklaracije)
                    .Where(o => o.Aktivna && o.MfpDeklaracije.Count > 0)
                    .ToDictionary(
                        o => o.Sifra,
                        o => (IReadOnlyList<OlaksicaMfp>)o.MfpDeklaracije.OrderBy(m => m.Oznaka).ToList(),
                        StringComparer.Ordinal);
            }
            catch
            {
                // Baza starije verzije nema šifarnik olakšica — prijava ide bez MFP-a, kao ranije.
                return new Dictionary<string, IReadOnlyList<OlaksicaMfp>>();
            }
        }
    }

    private void PreuzmiSedisteIzFirme()
    {
        try
        {
            var sifra = _db.Firme.FirstOrDefault()?.SifraOpstine;
            if (!string.IsNullOrWhiteSpace(sifra)) Sediste = sifra;
        }
        catch { }
    }

    // Podaci koji ne postoje u tabeli Firma (ili ih korisnik ovde ručno prilagođava
    // za potrebe PPP-PD prijave) čuvaju se u korisničkim postavkama da se ne bi
    // gubili pri svakom ponovnom otvaranju stranice.
    private void UcitajSacuvanePostavke()
    {
        var s = UserSettings.Instance;
        if (!string.IsNullOrWhiteSpace(s.PppPdSediste)) Sediste = s.PppPdSediste;
        if (!string.IsNullOrWhiteSpace(s.PppPdTelefon)) Telefon = s.PppPdTelefon;
        if (!string.IsNullOrWhiteSpace(s.PppPdAdresa)) Adresa = s.PppPdAdresa;
        if (!string.IsNullOrWhiteSpace(s.PppPdEmail)) Email = s.PppPdEmail;
        if (!string.IsNullOrWhiteSpace(s.PppPdVrstaPrijave)) SelectedVrstaPrijave = s.PppPdVrstaPrijave;
        if (!string.IsNullOrWhiteSpace(s.PppPdOznakaZaKonacnu)) SelectedOznakaZaKonacnu = s.PppPdOznakaZaKonacnu;
        if (!string.IsNullOrWhiteSpace(s.PppPdNajnizaOsnovica)) SelectedNajnizaOsnovica = s.PppPdNajnizaOsnovica;
        if (!string.IsNullOrWhiteSpace(s.PppPdTipIsplatioca)) SelectedTipIsplatioca = s.PppPdTipIsplatioca;
    }

    private void SacuvajPostavke()
    {
        var s = UserSettings.Instance;
        s.PppPdSediste = Sediste;
        s.PppPdTelefon = Telefon;
        s.PppPdAdresa = Adresa;
        s.PppPdEmail = Email;
        s.PppPdVrstaPrijave = SelectedVrstaPrijave;
        s.PppPdOznakaZaKonacnu = SelectedOznakaZaKonacnu;
        s.PppPdNajnizaOsnovica = SelectedNajnizaOsnovica;
        s.PppPdTipIsplatioca = SelectedTipIsplatioca;
        s.Save();
        StatusText = "Podaci o isplatiocu su sačuvani i biće automatski učitani sledeći put.";
    }

    private async Task InitAsync()
    {
        try
        {
            var god = await _db.ObracuniPlata
                .Select(o => o.Godina)
                .Distinct()
                .OrderByDescending(g => g)
                .ToListAsync();

            if (god.Count == 0)
                god = [DateTime.Now.Year];

            Godine = new ObservableCollection<int>(god);

            if (AppConfig.ActiveGodina.HasValue && Godine.Contains(AppConfig.ActiveGodina.Value))
            {
                SelectedGodina = AppConfig.ActiveGodina.Value;
            }
            else
            {
                SelectedGodina = Godine.FirstOrDefault();
            }

            await LoadObracuneAsync();
        }
        catch (Exception ex)
        {
            StatusText = $"Greška: {ex.Message}";
        }
    }

    public async Task LoadObracuneAsync()
    {
        try
        {
            StatusText = "Učitavanje obračuna za izabrani period...";
            ValidationAlerts.Clear();
            PrikaziUpozorenja = false;
            PodaciSuValidni = false;

            UcitajIsplate();

            // Stornirani obračun se ne prijavljuje. Ako je već bio u podnetoj prijavi,
            // uklanja se izmenjenom prijavom — a izmenjena prijava je upravo ovo, bez njega.
            //
            // Prijava se podnosi za jednu isplatu (Faza 2.2): akontacija i konačna isplata
            // istog meseca su dve prijave, svaka sa svojim datumom plaćanja i svojim BOP-om.
            var sviUPeriodu = await IsplataService
                .Obuhvat(
                    _db.ObracuniPlata
                        .Include(o => o.Radnik)
                        // Bez vrste ugovora naknada van radnog odnosa dobija šifru zarade.
                        .Include(o => o.Ugovor!).ThenInclude(u => u.VrstaUgovora),
                    SelectedGodina, SelectedMesec, _izabranaIsplata)
                .OrderBy(o => o.Radnik.BrojRadnika)
                .ToListAsync();

            var list = sviUPeriodu.Where(o => !o.Storniran).ToList();
            BrojStorniranih = sviUPeriodu.Count - list.Count;

            Obracuni = new ObservableCollection<ObracunPlate>(list);
            
            // Izračunaj zbirne KPI vrednosti
            TotalRadnika = list.Count;
            UkupnoBruto = list.Sum(o => o.BrutoZarada + o.BrutoBolovanje);
            UkupnoPorez = list.Sum(o => o.PorezNaDohodak);
            
            // Ukupni doprinosi (PIO + ZDR + NEZ) i za radnika i za poslodavca
            UkupnoDoprinosi = list.Sum(o => 
                o.DoprinosPioRadnik + o.DoprinosZdravstvoRadnik + o.DoprinosNezaposlenostRadnik +
                o.DoprinosPioPoslodavac + o.DoprinosZdravstvoPoslodavac + o.DoprinosNezaposlenostPoslodavac);

            string obuhvat = _izabranaIsplata == null || _izabranaIsplata.JePrva
                ? $"{SelectedMesec:D2}.{SelectedGodina}"
                : $"{SelectedMesec:D2}.{SelectedGodina} — {_izabranaIsplata.Naziv}";

            StatusText = BrojStorniranih > 0
                ? $"Učitano {list.Count} obračuna za {obuhvat}; storniranih izostavljeno: {BrojStorniranih}."
                : $"Učitano {list.Count} obračuna za {obuhvat}.";

            // Automatski pokreni tihu validaciju
            ValidateDataSilent();
        }
        catch (Exception ex)
        {
            StatusText = $"Greška pri učitavanju: {ex.Message}";
        }
    }

    private void ValidateDataSilent()
    {
        ValidationAlerts.Clear();
        
        if (Obracuni.Count == 0)
        {
            ValidationAlerts.Add("Nema obračunatih zarada za izabrani mesec i godinu.");
            PodaciSuValidni = false;
            return;
        }

        foreach (var o in Obracuni)
        {
            if (o.Radnik == null) continue;
            
            // 1. Provera JMBG-a
            string jmbg = o.Radnik.Jmbg?.Trim() ?? "";
            if (string.IsNullOrEmpty(jmbg))
            {
                ValidationAlerts.Add($"Zaposleni {o.Radnik.ImeIPrezime} nema upisan JMBG!");
            }
            else
            {
                if (UserSettings.Instance.ValidacijaJmbgOmogucena)
                {
                    if (!JmbgValidator.Validate(jmbg, out string jmbgError))
                    {
                        ValidationAlerts.Add($"Zaposleni {o.Radnik.ImeIPrezime} ima neispravan JMBG: {jmbgError}");
                    }
                }
                else if (jmbg.Length != 13 || !jmbg.All(char.IsDigit))
                {
                    ValidationAlerts.Add($"Zaposleni {o.Radnik.ImeIPrezime} ima neispravan format JMBG-a (mora imati tačno 13 cifara).");
                }
            }
            
            // 2. Provera imena i prezimena
            var parts = o.Radnik.ImeIPrezime.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                ValidationAlerts.Add($"Ime zaposlenog '{o.Radnik.ImeIPrezime}' možda ne sadrži i ime i prezime odvojeno razmakom.");
            }
            
            // 3. Provera bruto vrednosti
            decimal bruto = o.BrutoZarada + o.BrutoBolovanje;
            if (bruto <= 0)
            {
                ValidationAlerts.Add($"Zaposleni {o.Radnik.ImeIPrezime} ima obračun sa nultim ili negativnim bruto iznosom ({bruto:N2} RSD).");
            }
        }

        PodaciSuValidni = ValidationAlerts.Count == 0;
    }

    public void ValidateData()
    {
        ValidateDataSilent();
        PrikaziUpozorenja = true;
        
        if (PodaciSuValidni)
        {
            StatusText = "Svi podaci su uspešno validirani i spremni za izvoz.";
        }
        else
        {
            StatusText = $"Validacija završena. Pronađeno je {ValidationAlerts.Count} upozorenja.";
        }
    }

    // ── PROPERTIJI SA NOTIFIKACIJOM ───────────────────────
    public ObservableCollection<int> Godine
    {
        get => _godine;
        set { _godine = value; OnPropertyChanged(); }
    }

    public ObservableCollection<int> Meseci
    {
        get => _meseci;
        set { _meseci = value; OnPropertyChanged(); }
    }

    public int SelectedGodina
    {
        get => _selectedGodina;
        set
        {
            _selectedGodina = value;
            OnPropertyChanged();
            AppConfig.ActiveGodina = value;
            AzurirajBrojKalendarskihDana();
            _ = LoadObracuneAsync();
        }
    }

    public int SelectedMesec
    {
        get => _selectedMesec;
        set
        {
            _selectedMesec = value;
            OnPropertyChanged();
            AppConfig.ActiveMesec = value;
            AzurirajBrojKalendarskihDana();
            _ = LoadObracuneAsync();
        }
    }

    private void AzurirajBrojKalendarskihDana()
    {
        if (_selectedGodina > 0 && _selectedMesec is >= 1 and <= 12)
        {
            int danaUMesecu = DateTime.DaysInMonth(_selectedGodina, _selectedMesec);
            BrojKalendarskihDana = danaUMesecu;
            DatumPlacanja = new DateTime(_selectedGodina, _selectedMesec, danaUMesecu);
        }
    }

    public ObservableCollection<ObracunPlate> Obracuni
    {
        get => _obracuni;
        set { _obracuni = value; OnPropertyChanged(); }
    }

    // ── Isplate u mesecu (Faza 2.2) ───────────────────────
    public ObservableCollection<Isplata> Isplate
    {
        get => _isplate;
        private set { _isplate = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Isplata za koju se prijava podnosi. Menja i obuhvat obračuna i oznaku za konačnu
    /// isplatu (PP 1.4) — akontacija se prijavljuje sa „N", jer posle nje sledi konačan
    /// obračun istog prihoda.
    /// </summary>
    public Isplata? IzabranaIsplata
    {
        get => _izabranaIsplata;
        set
        {
            if (ReferenceEquals(_izabranaIsplata, value)) return;

            _izabranaIsplata = value;
            OnPropertyChanged();

            if (value != null)
            {
                if (value.DatumIsplate != default) DatumPlacanja = value.DatumIsplate;

                // Zapamćena postavka važi za mesec sa jednom isplatom, kakav je i bio pre
                // Faze 2.2. Za dodatnu isplatu je merodavna njena vrsta — korisnik oznaku i
                // dalje može promeniti u padajućoj listi.
                if (!value.JePrva) SelectedOznakaZaKonacnu = value.OznakaZaKonacnuIsplatu;
            }

            _ = LoadObracuneAsync();
        }
    }

    /// <summary>Selektor isplate ima smisla tek kad ih mesec ima više od jedne.</summary>
    public bool ImaViseIsplata => _isplate.Count > 1;

    private void UcitajIsplate()
    {
        List<Isplata> isplate;

        // Period se popunjava u dva koraka, pa se prvo učitavanje dešava pre nego što je
        // godina poznata. Tada nema šta da se obezbedi.
        if (SelectedGodina <= 0 || SelectedMesec is < 1 or > 12)
        {
            Isplate = [];
            OnPropertyChanged(nameof(ImaViseIsplata));
            return;
        }

        try
        {
            _isplataService.Obezbedi(SelectedGodina, SelectedMesec);
            isplate = _isplataService.Isplate(SelectedGodina, SelectedMesec).ToList();
        }
        catch (Exception ex)
        {
            // Baza starije verzije nema tabelu isplata — prijava se pravi nad celim periodom.
            Serilog.Log.Warning(ex, "Isplate se ne mogu učitati za {Godina}/{Mesec}", SelectedGodina, SelectedMesec);
            isplate = [];
        }

        // Isplata izabrana za drugi mesec ne važi za ovaj; bira se prva, kao i do sada.
        if (_izabranaIsplata == null
            || _izabranaIsplata.Godina != SelectedGodina
            || _izabranaIsplata.Mesec != SelectedMesec)
        {
            _izabranaIsplata = isplate.FirstOrDefault();
            OnPropertyChanged(nameof(IzabranaIsplata));
        }

        Isplate = new ObservableCollection<Isplata>(isplate);
        OnPropertyChanged(nameof(ImaViseIsplata));
    }

    public string Pib
    {
        get => _pib;
        set { _pib = value; OnPropertyChanged(); }
    }

    public string MaticniBroj
    {
        get => _maticniBroj;
        set { _maticniBroj = value; OnPropertyChanged(); }
    }

    public string Naziv
    {
        get => _naziv;
        set { _naziv = value; OnPropertyChanged(); }
    }

    public string Telefon
    {
        get => _telefon;
        set { _telefon = value; OnPropertyChanged(); }
    }

    public string Adresa
    {
        get => _adresa;
        set { _adresa = value; OnPropertyChanged(); }
    }

    public string Email
    {
        get => _email;
        set { _email = value; OnPropertyChanged(); }
    }

    public string Sediste
    {
        get => _sediste;
        set { _sediste = value; OnPropertyChanged(); }
    }

    public DateTime DatumPlacanja
    {
        get => _datumPlacanja;
        set 
        { 
            _datumPlacanja = value; 
            OnPropertyChanged(); 
            // Ažuriraj klijentsku oznaku na osnovu novog datuma plaćanja
            KlijentskaOznaka = $"DECL-{value:ddMMyyyy}";
        }
    }

    public string SelectedVrstaPrijave
    {
        get => _selectedVrstaPrijave;
        set { _selectedVrstaPrijave = value; OnPropertyChanged(); }
    }

    public string SelectedOznakaZaKonacnu
    {
        get => _selectedOznakaZaKonacnu;
        set { _selectedOznakaZaKonacnu = value; OnPropertyChanged(); }
    }

    public string SelectedNajnizaOsnovica
    {
        get => _selectedNajnizaOsnovica;
        set { _selectedNajnizaOsnovica = value; OnPropertyChanged(); }
    }

    public string SelectedTipIsplatioca
    {
        get => _selectedTipIsplatioca;
        set { _selectedTipIsplatioca = value; OnPropertyChanged(); }
    }

    public string KlijentskaOznaka
    {
        get => _klijentskaOznaka;
        set { _klijentskaOznaka = value; OnPropertyChanged(); }
    }

    public int BrojKalendarskihDana
    {
        get => _brojKalendarskihDana;
        set { _brojKalendarskihDana = value; OnPropertyChanged(); }
    }

    /// <summary>Koliko je obračuna iz perioda izostavljeno jer su stornirani.</summary>
    public int BrojStorniranih
    {
        get => _brojStorniranih;
        set
        {
            _brojStorniranih = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ImaStorniranih));
        }
    }

    public bool ImaStorniranih => _brojStorniranih > 0;

    // ── Izmenjena prijava ─────────────────────────────────
    /// <summary>
    /// Ponuđene vrste izmene za padajuću listu. Prazna vrednost znači redovnu prijavu —
    /// tada se elementi izmene uopšte ne emituju.
    /// </summary>
    public IReadOnlyList<VrstaIzmenePrijave> VrsteIzmene { get; } =
        [VrstaIzmenePrijave.Nema, VrstaIzmenePrijave.Izmena,
         VrstaIzmenePrijave.PoNalazuKontrole, VrstaIzmenePrijave.PoNaloguSuda];

    public IReadOnlyList<OsnovIzmenePrijave> OsnoviIzmene { get; } =
        [OsnovIzmenePrijave.Nema, OsnovIzmenePrijave.ZalbaPrviStepen,
         OsnovIzmenePrijave.ZalbaDrugiStepen, OsnovIzmenePrijave.PoNaloguSuda];

    public VrstaIzmenePrijave VrstaIzmene
    {
        get => _vrstaIzmene;
        set
        {
            _vrstaIzmene = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(JeIzmenjenaPrijava));
        }
    }

    /// <summary>Da li se ovom prijavom menja ranije podneta — otključava polja 1.5a–1.6a.</summary>
    public bool JeIzmenjenaPrijava => _vrstaIzmene != VrstaIzmenePrijave.Nema;

    /// <summary>JIPD prijave koja se menja (PP 1.5a). Obavezan kad je izabrana vrsta izmene.</summary>
    public string JipdKojiSeMenja
    {
        get => _jipdKojiSeMenja;
        set { _jipdKojiSeMenja = value; OnPropertyChanged(); }
    }

    public string BrojResenja
    {
        get => _brojResenja;
        set { _brojResenja = value; OnPropertyChanged(); }
    }

    public OsnovIzmenePrijave OsnovIzmene
    {
        get => _osnovIzmene;
        set { _osnovIzmene = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Podaci o izmeni onako kako ih traži generator; <c>null</c> za redovnu prijavu.
    /// </summary>
    public IzmenaPrijave? Izmena => JeIzmenjenaPrijava
        ? new IzmenaPrijave
        {
            VrstaIzmene = VrstaIzmene,
            Jipd = JipdKojiSeMenja,
            BrojResenja = BrojResenja,
            Osnov = OsnovIzmene
        }
        : null;

    public int TotalRadnika
    {
        get => _totalRadnika;
        set { _totalRadnika = value; OnPropertyChanged(); }
    }

    public decimal UkupnoBruto
    {
        get => _ukupnoBruto;
        set { _ukupnoBruto = value; OnPropertyChanged(); }
    }

    public decimal UkupnoPorez
    {
        get => _ukupnoPorez;
        set { _ukupnoPorez = value; OnPropertyChanged(); }
    }

    public decimal UkupnoDoprinosi
    {
        get => _ukupnoDoprinosi;
        set { _ukupnoDoprinosi = value; OnPropertyChanged(); }
    }

    public string StatusText
    {
        get => _statusText;
        set { _statusText = value; OnPropertyChanged(); }
    }

    public ObservableCollection<string> ValidationAlerts
    {
        get => _validationAlerts;
        set { _validationAlerts = value; OnPropertyChanged(); }
    }

    public bool PrikaziUpozorenja
    {
        get => _prikaziUpozorenja;
        set { _prikaziUpozorenja = value; OnPropertyChanged(); }
    }

    public bool PodaciSuValidni
    {
        get => _podaciSuValidni;
        set { _podaciSuValidni = value; OnPropertyChanged(); }
    }

    public ICommand LoadCommand { get; }
    public ICommand ValidateCommand { get; }
    public ICommand SaveCommand { get; private set; } = null!;

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
