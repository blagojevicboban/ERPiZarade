using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.EntityFrameworkCore;
using ERPiZaradeApp.Services;
using ERPiZaradeApp.Views.Radnici;
using ERPiZaradeData;
using ERPiZaradeData.Models;

namespace ERPiZaradeApp.Views.Knjizenje;

/// <summary>
/// Nalog za knjiženje obračuna zarada (Faza 3.1).
///
/// Ekran ništa ne upisuje u bazu zarada — nalog je izveden iz obračuna i pravi se svaki put
/// iznova. Zato se izmena konta u šifarniku odmah vidi, a pogrešno izvezen nalog se ispravlja
/// ponovnim izvozom umesto storniranjem.
/// </summary>
public class KnjizenjeViewModel : INotifyPropertyChanged
{
    private readonly PlataDbContext _db;
    private readonly KnjizenjeService _knjizenje;
    private readonly IsplataService _isplataService;

    private int _godina;
    private int _mesec;
    private DateTime _datum = DateTime.Today;
    private Isplata? _izabranaIsplata;
    private NalogZaKnjizenje? _nalog;
    private string _statusTekst = "";

    public KnjizenjeViewModel()
    {
        _db = PlataDbContext.Create(AppConfig.DbPath);
        _knjizenje = new KnjizenjeService(_db);
        _isplataService = new IsplataService(_db);

        _godina = AppConfig.ActiveGodina ?? DateTime.Now.Year;
        _mesec = AppConfig.ActiveMesec ?? DateTime.Now.Month;

        PripremiCommand = new RelayCommand(_ => Pripremi());
        IzvoziJsonCommand = new RelayCommand(_ => Izvezi(jeJson: true), _ => SmeSeIzvesti);
        IzvoziCsvCommand = new RelayCommand(_ => Izvezi(jeJson: false), _ => ImaStavke);

        UcitajIsplate();
        Pripremi();
    }

    // ── Stanje ───────────────────────────────────────────────────────
    public ObservableCollection<StavkaKnjizenja> Stavke { get; } = [];
    public ObservableCollection<NalazProvere> Nalazi { get; } = [];
    public ObservableCollection<Isplata> Isplate { get; } = [];

    /// <summary>
    /// Isplata za koju se nalog pravi. Svaka isplata meseca je zaseban dokument sa svojim
    /// datumom — akontacija i konačna zarada se ne knjiže jednim nalogom.
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
                _datum = value.DatumIsplate;
                OnPropertyChanged(nameof(Datum));
            }

            Pripremi();
        }
    }

    public bool ImaViseIsplata => Isplate.Count > 1;

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

    public DateTime Datum
    {
        get => _datum;
        set { _datum = value; OnPropertyChanged(); Pripremi(); }
    }

    public string StatusTekst
    {
        get => _statusTekst;
        private set { _statusTekst = value; OnPropertyChanged(); }
    }

    // ── Zbirovi ──────────────────────────────────────────────────────
    public string OpisNaloga => _nalog?.Opis ?? "";
    public decimal UkupnoDuguje => _nalog?.UkupnoDuguje ?? 0m;
    public decimal UkupnoPotrazuje => _nalog?.UkupnoPotrazuje ?? 0m;
    public decimal Razlika => _nalog?.Razlika ?? 0m;
    public bool JeUravnotezen => _nalog?.JeUravnotezen ?? false;
    public int BrojObracuna => _nalog?.BrojObracuna ?? 0;
    public bool SmeSeIzvesti => _nalog?.SmeSeIzvesti ?? false;
    public bool ImaStavke => Stavke.Count > 0;

    /// <summary>Ravnoteža je jedina stvar koju korisnik mora da vidi pre izvoza.</summary>
    public string RavnotezaTekst => _nalog == null || _nalog.Stavke.Count == 0
        ? "—"
        : _nalog.JeUravnotezen ? "✔ u ravnoteži" : $"✘ razlika {_nalog.Razlika:N2}";

    public ICommand PripremiCommand { get; }
    public ICommand IzvoziJsonCommand { get; }
    public ICommand IzvoziCsvCommand { get; }

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
            _isplataService.Obezbedi(Godina, Mesec);
            foreach (var i in _isplataService.Isplate(Godina, Mesec)) Isplate.Add(i);
        }
        catch (Exception ex)
        {
            // Baza starije verzije nema tabelu isplata — nalog se pravi nad celim periodom.
            Serilog.Log.Warning(ex, "Isplate se ne mogu učitati za {Godina}/{Mesec}", Godina, Mesec);
        }

        _izabranaIsplata = Isplate.FirstOrDefault();
        OnPropertyChanged(nameof(IzabranaIsplata));
        OnPropertyChanged(nameof(ImaViseIsplata));

        if (_izabranaIsplata is { DatumIsplate: var d } && d != default) _datum = d;
    }

    private void Pripremi()
    {
        _nalog = _knjizenje.Pripremi(Godina, Mesec, _izabranaIsplata, Datum);

        Stavke.Clear();
        foreach (var s in _nalog.Stavke) Stavke.Add(s);

        Nalazi.Clear();
        foreach (var n in _nalog.Nalazi.OrderByDescending(x => x.Tezina).ThenBy(x => x.BrojRadnika))
            Nalazi.Add(n);

        OsveziZbirove();

        StatusTekst = _nalog.SmeSeIzvesti
            ? $"Nalog za {_nalog.BrojObracuna} obračuna, {_nalog.Stavke.Count} stavki, {_nalog.UkupnoDuguje:N2}."
            : _nalog.Stavke.Count == 0
                ? "Nema šta da se knjiži."
                : $"Nalog nije spreman — {_nalog.BrojGresaka} grešaka.";
    }

    /// <summary>
    /// Zapisuje nalog u fajl. JSON ide u ERPiFinansije, CSV u tabelu za proveru; zato se
    /// CSV sme snimiti i kad nalog nije spreman — upravo se u njemu i traži gde je razlika.
    /// </summary>
    private void Izvezi(bool jeJson)
    {
        if (_nalog == null || _nalog.Stavke.Count == 0) return;

        string sadrzaj;

        if (jeJson)
        {
            var firma = _db.Firme.AsNoTracking().FirstOrDefault();
            sadrzaj = NalogKnjizenjaWriter.Generisi(_nalog, firma, out var nalaziZapisa);

            var greske = nalaziZapisa.Where(n => n.Tezina == TezinaNalaza.Greska).ToList();
            if (greske.Count > 0)
            {
                System.Windows.MessageBox.Show(
                    "Fajl nije snimljen jer bi ga glavna knjiga odbila:\n\n" +
                    string.Join(Environment.NewLine, greske.Take(10).Select(n => $"• {n.Provera}: {n.Opis}")),
                    "Izvoz zaustavljen", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                StatusTekst = $"Izvoz zaustavljen — {greske.Count} grešaka u nalogu.";
                return;
            }
        }
        else
        {
            sadrzaj = NalogKnjizenjaWriter.GenerisiCsv(_nalog);
        }

        var sfd = new Microsoft.Win32.SaveFileDialog
        {
            Filter = jeJson ? "JSON fajl (*.json)|*.json" : "CSV fajl (*.csv)|*.csv",
            FileName = KnjizenjeService.ImeFajla(_nalog, jeJson ? "json" : "csv"),
            Title = jeJson ? "Sačuvaj nalog za uvoz u ERPiFinansije" : "Sačuvaj nalog za proveru u tabeli"
        };

        if (sfd.ShowDialog() != true) return;

        // CSV se otvara u Excelu, koji bez BOM-a naše znakove prikazuje kao smeće.
        var kodiranje = jeJson
            ? new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
            : new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: true);

        System.IO.File.WriteAllText(sfd.FileName, sadrzaj, kodiranje);

        AuditService.Zabelezi(_db, Godina, Mesec, AkcijaObracuna.NalogZaKnjizenje,
            $"Izvezen nalog za knjiženje ({(jeJson ? "JSON" : "CSV")}) — {_nalog.Stavke.Count} stavki, " +
            $"duguje {_nalog.UkupnoDuguje:N2}, potražuje {_nalog.UkupnoPotrazuje:N2}");

        StatusTekst = $"Snimljeno {_nalog.Stavke.Count} stavki u {sfd.FileName}.";
    }

    private void OsveziZbirove()
    {
        OnPropertyChanged(nameof(OpisNaloga));
        OnPropertyChanged(nameof(UkupnoDuguje));
        OnPropertyChanged(nameof(UkupnoPotrazuje));
        OnPropertyChanged(nameof(Razlika));
        OnPropertyChanged(nameof(JeUravnotezen));
        OnPropertyChanged(nameof(RavnotezaTekst));
        OnPropertyChanged(nameof(BrojObracuna));
        OnPropertyChanged(nameof(SmeSeIzvesti));
        OnPropertyChanged(nameof(ImaStavke));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
