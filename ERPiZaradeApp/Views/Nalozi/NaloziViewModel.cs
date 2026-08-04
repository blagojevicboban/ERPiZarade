using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using ERPiZaradeApp.Services;
using ERPiZaradeApp.Views.Radnici;
using ERPiZaradeData;
using ERPiZaradeData.Models;

namespace ERPiZaradeApp.Views.Nalozi;

/// <summary>
/// Priprema naloga za prenos za aktivni obračunski period.
///
/// Redosled je namerno vezan: nalog za poreze i doprinose se ne može formirati dok se ne
/// učita dokument koji ePorezi izda po prihvatanju PPP-PD prijave, jer BOP iz njega je
/// poziv na broj bez kog uplata ostaje neraspoređena.
/// </summary>
public class NaloziViewModel : INotifyPropertyChanged
{
    private readonly PlataDbContext _db;
    private readonly NalogZaPrenosService _nalogService;
    private readonly IsplataService _isplataService;

    private int _godina;
    private int _mesec;
    private DateTime _datumValute = DateTime.Today;
    private PppPdPrijava? _prijava;
    private Isplata? _izabranaIsplata;
    private PaketNaloga? _paket;
    private string _statusTekst = "";

    /// <summary>
    /// Rod isplata za koje se pripremaju nalozi. Zarada i naknada van radnog odnosa se
    /// prijavljuju zasebno, pa i uplata poreza i doprinosa ide po zasebnom BOP-u — mešanje bi
    /// jednom uplatom pokrilo dve deklaracije.
    /// </summary>
    private readonly RodIsplate _rod;

    public NaloziViewModel(RodIsplate rod = RodIsplate.Zarada)
    {
        _rod = rod;
        _db = PlataDbContext.Create(AppConfig.DbPath);
        _nalogService = new NalogZaPrenosService(_db);
        _isplataService = new IsplataService(_db);

        _godina = AppConfig.ActiveGodina ?? DateTime.Now.Year;
        _mesec = AppConfig.ActiveMesec ?? DateTime.Now.Month;

        UcitajEPoreziCommand = new RelayCommand(_ => UcitajEPoreziDokument());
        PripremiCommand = new RelayCommand(_ => Pripremi());
        IzvoziHalcomCommand = new RelayCommand(_ => Izvezi(FormatIzvoza.Halcom), _ => SmeSePoslatiUBanku);
        IzvoziTrezorCommand = new RelayCommand(_ => Izvezi(FormatIzvoza.Trezor), _ => SmeSePoslatiUBanku);

        UcitajIsplate();
        UcitajPrijavu();
        Pripremi();
    }

    // ── Stanje ───────────────────────────────────────────────────────
    public ObservableCollection<NalogZaPrenos> Nalozi { get; } = [];
    public ObservableCollection<NalazProvere> Nalazi { get; } = [];
    public ObservableCollection<Isplata> Isplate { get; } = [];

    /// <summary>
    /// Isplata za koju se paket pravi (Faza 2.2). Menja i prijavu iz koje se uzima BOP —
    /// svaka isplata ima svoju, pa BOP druge isplate na nalogu prve šalje novac na tuđu
    /// deklaraciju.
    /// </summary>
    public Isplata? IzabranaIsplata
    {
        get => _izabranaIsplata;
        set
        {
            if (ReferenceEquals(_izabranaIsplata, value)) return;

            _izabranaIsplata = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ImaViseIsplata));

            if (value != null && value.DatumIsplate != default)
            {
                _datumValute = value.DatumIsplate;
                OnPropertyChanged(nameof(DatumValute));
            }

            UcitajPrijavu();
            OsveziPodatkePrijave();
            Pripremi();
        }
    }

    /// <summary>Selektor isplate ima smisla tek kad ih je više od jedne.</summary>
    public bool ImaViseIsplata => Isplate.Count > 1;

    private void UcitajIsplate()
    {
        Isplate.Clear();

        if (Godina <= 0 || Mesec is < 1 or > 12)
        {
            OnPropertyChanged(nameof(ImaViseIsplata));
            return;
        }

        try
        {
            // Prvu isplatu zarade program pravi sam; isplatu naknada ne — nju određuje datum
            // plaćanja, koji se ne može pogoditi.
            if (_rod == RodIsplate.Zarada) _isplataService.Obezbedi(Godina, Mesec);

            foreach (var i in _isplataService.Isplate(Godina, Mesec, _rod)) Isplate.Add(i);
        }
        catch (Exception ex)
        {
            // Baza starije verzije nema tabelu isplata — nalozi se prave nad celim periodom.
            Serilog.Log.Warning(ex, "Isplate se ne mogu učitati za {Godina}/{Mesec}", Godina, Mesec);
        }

        _izabranaIsplata = Isplate.FirstOrDefault();
        OnPropertyChanged(nameof(IzabranaIsplata));
        OnPropertyChanged(nameof(ImaViseIsplata));

        if (_izabranaIsplata is { DatumIsplate: var d } && d != default) _datumValute = d;
    }

    public int Godina
    {
        get => _godina;
        private set { _godina = value; OnPropertyChanged(); OnPropertyChanged(nameof(PeriodTekst)); }
    }

    public int Mesec
    {
        get => _mesec;
        private set { _mesec = value; OnPropertyChanged(); OnPropertyChanged(nameof(PeriodTekst)); }
    }

    public string PeriodTekst => $"{Mesec:D2}/{Godina}";

    public DateTime DatumValute
    {
        get => _datumValute;
        set { _datumValute = value; OnPropertyChanged(); Pripremi(); }
    }

    public string StatusTekst
    {
        get => _statusTekst;
        private set { _statusTekst = value; OnPropertyChanged(); }
    }

    // ── Podaci prihvaćene prijave ────────────────────────────────────
    public string Bop => _prijava?.Bop ?? "";
    public decimal IznosZaUplatu => _prijava?.IznosZaUplatu ?? 0m;
    public string RacunZaUplatu => _prijava?.RacunZaUplatu ?? "";
    public string ModelPozivaNaBroj => _prijava?.ModelPozivaNaBroj ?? "";
    public string StatusPrijaveTekst => _prijava == null ? "nije učitana" : _prijava.Status.ToString();
    public bool ImaPrijavu => _prijava != null && !string.IsNullOrWhiteSpace(_prijava.Bop);

    // ── Zbirovi ──────────────────────────────────────────────────────
    public decimal ZbirZarada => _paket?.ZbirZarada ?? 0m;
    public decimal ZbirPorezaIDoprinosa => _paket?.ZbirPorezaIDoprinosa ?? 0m;
    public decimal Ukupno => _paket?.Ukupno ?? 0m;
    public int BrojNaloga => _paket?.Nalozi.Count ?? 0;
    public bool SmeSePoslatiUBanku => _paket?.SmeSePoslatiUBanku ?? false;

    public ICommand UcitajEPoreziCommand { get; }
    public ICommand PripremiCommand { get; }
    public ICommand IzvoziHalcomCommand { get; }
    public ICommand IzvoziTrezorCommand { get; }

    private enum FormatIzvoza { Halcom, Trezor }

    /// <summary>Dodatak imenu fajla kad mesec ima više isplata; prazan kad je isplata jedna.</summary>
    private string SufiksIsplate
        => _izabranaIsplata == null || _izabranaIsplata.JePrva ? "" : $"_isplata{_izabranaIsplata.RedniBroj}";

    /// <summary>
    /// Zapisuje naloge u fajl za bankarsku aplikaciju. Ako zapisivač prijavi nalaz koji bi
    /// doveo do odbijanja fajla, fajl se <b>ne snima</b> — bolje nego da se otkrije tek
    /// pri učitavanju u banci, gde poruka o grešci obično ne kaže koji je nalog sporan.
    /// </summary>
    private void Izvezi(FormatIzvoza format)
    {
        if (_paket == null || _paket.Nalozi.Count == 0) return;

        bool jeHalcom = format == FormatIzvoza.Halcom;

        byte[] sadrzaj;
        IReadOnlyList<NalazProvere> nalaziZapisa;

        if (jeHalcom)
        {
            sadrzaj = HalcomPpzWriter.Generisi(_paket.Nalozi, out nalaziZapisa);
        }
        else
        {
            string json = TrezorEppWriter.Generisi(_paket.Nalozi, out nalaziZapisa);
            sadrzaj = System.Text.Encoding.UTF8.GetBytes(json);
        }

        var greske = nalaziZapisa.Where(n => n.Tezina == TezinaNalaza.Greska).ToList();
        if (greske.Count > 0)
        {
            System.Windows.MessageBox.Show(
                "Fajl nije snimljen jer bi ga bankarska aplikacija odbila:\n\n" +
                string.Join(Environment.NewLine, greske.Take(10).Select(n => $"• {n.Provera}: {n.Opis}")),
                "Izvoz zaustavljen", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            StatusTekst = $"Izvoz zaustavljen — {greske.Count} grešaka u zapisu.";
            return;
        }

        var sfd = new Microsoft.Win32.SaveFileDialog
        {
            Filter = jeHalcom ? "Tekstualni fajl (*.txt)|*.txt" : "JSON fajl (*.json)|*.json",
            // Kad mesec ima više isplata, dva fajla istog imena bi se lako pomešala u banci.
            FileName = jeHalcom
                ? $"Nalozi_{Godina}_{Mesec:D2}{SufiksIsplate}.txt"
                : $"Nalozi_{Godina}_{Mesec:D2}{SufiksIsplate}.json",
            Title = jeHalcom ? "Sačuvaj naloge za Hal E-Bank" : "Sačuvaj naloge za trezorski ePP"
        };

        if (sfd.ShowDialog() != true) return;

        System.IO.File.WriteAllBytes(sfd.FileName, sadrzaj);

        string tragIsplate = _izabranaIsplata == null || _izabranaIsplata.JePrva
            ? ""
            : $"Isplata: {_izabranaIsplata.Naziv}. ";

        AuditService.Zabelezi(_db, Godina, Mesec, AkcijaObracuna.PppPdGenerisan,
            $"{tragIsplate}Izvezeno {_paket.Nalozi.Count} naloga u {(jeHalcom ? "Hal E-Bank TXT" : "trezorski ePP JSON")}, ukupno {_paket.Ukupno:N2}");

        StatusTekst = $"Snimljeno {_paket.Nalozi.Count} naloga u {sfd.FileName}.";
    }

    /// <summary>Redni broj prijave koja pripada izabranoj isplati; 1 kad isplata nije poznata.</summary>
    private int RedniBrojPrijave => _izabranaIsplata?.RedniBroj ?? 1;

    private void UcitajPrijavu()
    {
        _prijava = _db.PppPdPrijave
            .FirstOrDefault(p => p.Godina == Godina && p.Mesec == Mesec && p.RedniBroj == RedniBrojPrijave);
    }

    /// <summary>
    /// Učitava XML koji ePorezi izda po prihvatanju prijave. Ništa se ne snima dok
    /// korisnik ne potvrdi pročitane vrednosti — pogrešan BOP šalje novac u prazno.
    /// </summary>
    private void UcitajEPoreziDokument()
    {
        var ofd = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "XML dokument (*.xml)|*.xml|Svi fajlovi (*.*)|*.*",
            Title = "Izaberite dokument koji je ePorezi izdao po prihvatanju PPP-PD prijave"
        };

        if (ofd.ShowDialog() != true) return;

        PodaciZaUplatu podaci;
        try
        {
            podaci = new EPoreziImportService().Ucitaj(ofd.FileName);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Dokument se ne može pročitati kao XML:\n\n{ex.Message}",
                "Greška pri čitanju", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            return;
        }

        if (!PotvrdiUcitano(podaci)) return;

        SacuvajPrijavu(podaci);
        UcitajPrijavu();
        OsveziPodatkePrijave();
        Pripremi();

        StatusTekst = $"Učitan BOP {podaci.Bop}, iznos {podaci.Iznos:N2}.";
    }

    /// <summary>Prikazuje šta je pročitano i šta nije, i traži potvrdu pre snimanja.</summary>
    private static bool PotvrdiUcitano(PodaciZaUplatu podaci)
    {
        if (!podaci.JeUpotrebljiv)
        {
            System.Windows.MessageBox.Show(
                "Iz dokumenta nisu pročitani podaci potrebni za nalog:\n\n" +
                string.Join(Environment.NewLine, podaci.NeprepoznataPolja.Select(p => $"• {p}")) +
                "\n\nDokument verovatno ima drugačiju strukturu od očekivane. " +
                "Podatke možete uneti ručno na ekranu PPP-PD prijave.",
                "Podaci nisu prepoznati", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            return false;
        }

        string poruka =
            $"Iz dokumenta je pročitano:\n\n" +
            $"• BOP: {podaci.Bop}\n" +
            $"• Iznos za uplatu: {podaci.Iznos:N2}\n" +
            $"• Uplatni račun: {podaci.RacunZaUplatu}\n" +
            $"• Model poziva na broj: {podaci.ModelPozivaNaBroj}\n";

        if (podaci.PopunjenaPodrazumevano.Count > 0)
        {
            poruka += "\nSledeće nije bilo u dokumentu, pa je popunjeno podrazumevanom vrednošću:\n" +
                      string.Join(Environment.NewLine, podaci.PopunjenaPodrazumevano.Select(p => $"• {p}")) + "\n";
        }

        poruka += "\nPotvrdite da su vrednosti tačne — po njima se formira nalog za uplatu.";

        return System.Windows.MessageBox.Show(poruka, "Potvrda učitanih podataka",
            System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question)
            == System.Windows.MessageBoxResult.Yes;
    }

    private void SacuvajPrijavu(PodaciZaUplatu podaci)
    {
        int redniBroj = RedniBrojPrijave;

        var prijava = _db.PppPdPrijave
            .FirstOrDefault(p => p.Godina == Godina && p.Mesec == Mesec && p.RedniBroj == redniBroj);

        bool nova = prijava == null;
        prijava ??= new PppPdPrijava { Godina = Godina, Mesec = Mesec, RedniBroj = redniBroj };

        prijava.Bop = podaci.Bop;
        prijava.IznosZaUplatu = podaci.Iznos;
        prijava.RacunZaUplatu = podaci.RacunZaUplatu;
        prijava.ModelPozivaNaBroj = podaci.ModelPozivaNaBroj;
        if (!string.IsNullOrWhiteSpace(podaci.Svrha)) prijava.SvrhaUplate = podaci.Svrha;

        // BOP se izdaje tek pošto Poreska uprava prijavu prihvati — njegovo prisustvo je
        // dokaz prihvatanja, pa se status postavlja iz samog dokumenta.
        prijava.Status = StatusPrijave.Prihvacena;
        prijava.DatumStatusa = DateTime.Now;

        if (nova) _db.PppPdPrijave.Add(prijava);
        _db.SaveChanges();

        AuditService.Zabelezi(_db, Godina, Mesec, AkcijaObracuna.PppPdGenerisan,
            $"Učitan BOP {podaci.Bop}, iznos za uplatu {podaci.Iznos:N2}");
    }

    private void Pripremi()
    {
        _paket = _nalogService.Pripremi(Godina, Mesec, _prijava, DatumValute, _izabranaIsplata);

        Nalozi.Clear();
        foreach (var n in _paket.Nalozi) Nalozi.Add(n);

        Nalazi.Clear();
        foreach (var n in _paket.Nalazi.OrderByDescending(x => x.Tezina).ThenBy(x => x.BrojRadnika))
            Nalazi.Add(n);

        OsveziZbirove();

        StatusTekst = _paket.SmeSePoslatiUBanku
            ? $"Pripremljeno {_paket.Nalozi.Count} naloga, ukupno {_paket.Ukupno:N2}."
            : $"Nalozi nisu spremni — {_paket.BrojGresaka} grešaka.";
    }

    private void OsveziZbirove()
    {
        OnPropertyChanged(nameof(ZbirZarada));
        OnPropertyChanged(nameof(ZbirPorezaIDoprinosa));
        OnPropertyChanged(nameof(Ukupno));
        OnPropertyChanged(nameof(BrojNaloga));
        OnPropertyChanged(nameof(SmeSePoslatiUBanku));
    }

    private void OsveziPodatkePrijave()
    {
        OnPropertyChanged(nameof(Bop));
        OnPropertyChanged(nameof(IznosZaUplatu));
        OnPropertyChanged(nameof(RacunZaUplatu));
        OnPropertyChanged(nameof(ModelPozivaNaBroj));
        OnPropertyChanged(nameof(StatusPrijaveTekst));
        OnPropertyChanged(nameof(ImaPrijavu));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
