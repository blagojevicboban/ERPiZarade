using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using LiveChartsCore;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using PlataData;
using SkiaSharp;

namespace PlataApp.Views.Dashboard;

public class DashboardViewModel : INotifyPropertyChanged
{
    private static readonly string[] NazivMeseca =
        ["Jan", "Feb", "Mar", "Apr", "Maj", "Jun", "Jul", "Avg", "Sep", "Okt", "Nov", "Dec"];

    private readonly PlataDbContext _db;

    private string _aktivniPeriodText = "Nije izabran";
    private int _ukupnoAktivnihRadnika;
    private decimal _ukupnaNetoMasa;
    private decimal _ukupnaBrutoMasa;
    private int _brojAktivnihKredita;
    private ObservableCollection<int> _godine = [];
    private int _selectedGodina;
    private ISeries[] _mesecniSeries = Array.Empty<ISeries>();
    private Axis[] _mesecniXAxes = Array.Empty<Axis>();
    private Axis[] _mesecniYAxes = Array.Empty<Axis>();

    public DashboardViewModel()
    {
        _db = PlataDbContext.Create(AppConfig.DbPath);
        UcitajPodatke();
        UcitajGodine();
    }

    public string AktivniPeriodText
    {
        get => _aktivniPeriodText;
        set { _aktivniPeriodText = value; OnPropertyChanged(); }
    }

    public int UkupnoAktivnihRadnika
    {
        get => _ukupnoAktivnihRadnika;
        set { _ukupnoAktivnihRadnika = value; OnPropertyChanged(); }
    }

    public decimal UkupnaNetoMasa
    {
        get => _ukupnaNetoMasa;
        set { _ukupnaNetoMasa = value; OnPropertyChanged(); }
    }

    public decimal UkupnaBrutoMasa
    {
        get => _ukupnaBrutoMasa;
        set { _ukupnaBrutoMasa = value; OnPropertyChanged(); }
    }

    public int BrojAktivnihKredita
    {
        get => _brojAktivnihKredita;
        set { _brojAktivnihKredita = value; OnPropertyChanged(); }
    }

    public ObservableCollection<int> Godine
    {
        get => _godine;
        set { _godine = value; OnPropertyChanged(); }
    }

    public int SelectedGodina
    {
        get => _selectedGodina;
        set
        {
            if (_selectedGodina == value) return;
            _selectedGodina = value;
            OnPropertyChanged();
            UcitajMesecniPregled(value);
        }
    }

    public ISeries[] MesecniSeries
    {
        get => _mesecniSeries;
        set { _mesecniSeries = value; OnPropertyChanged(); }
    }

    public Axis[] MesecniXAxes
    {
        get => _mesecniXAxes;
        set { _mesecniXAxes = value; OnPropertyChanged(); }
    }

    public Axis[] MesecniYAxes
    {
        get => _mesecniYAxes;
        set { _mesecniYAxes = value; OnPropertyChanged(); }
    }

    private void UcitajPodatke()
    {
        int godina;
        int mesec;

        if (AppConfig.ActiveGodina.HasValue && AppConfig.ActiveMesec.HasValue)
        {
            godina = AppConfig.ActiveGodina.Value;
            mesec = AppConfig.ActiveMesec.Value;
        }
        else
        {
            var poslednji = _db.Radnici
                .OrderByDescending(r => r.Godina)
                .ThenByDescending(r => r.Mesec)
                .Select(r => new { r.Godina, r.Mesec })
                .FirstOrDefault();
            godina = poslednji?.Godina ?? 0;
            mesec = poslednji?.Mesec ?? 0;
        }

        AktivniPeriodText = godina > 0 && mesec > 0 ? $"{mesec:D2}/{godina}" : "Nije izabran";

        var radniciPerioda = _db.Radnici
            .Where(r => r.Godina == godina && r.Mesec == mesec)
            .ToList();

        UkupnoAktivnihRadnika = radniciPerioda.Count(r => r.Aktivan);

        var obracuniPerioda = _db.ObracuniPlata
            .Where(o => o.Godina == godina && o.Mesec == mesec)
            .ToList();

        UkupnaNetoMasa = obracuniPerioda.Sum(o => o.NetoIsplata);
        UkupnaBrutoMasa = obracuniPerioda.Sum(o => o.Bruto2);
        BrojAktivnihKredita = _db.Krediti.Count(k => k.Aktivan);
    }

    private void UcitajGodine()
    {
        var godine = _db.Radnici
            .Select(r => r.Godina)
            .Distinct()
            .OrderByDescending(g => g)
            .ToList();

        var tekucaGodina = DateTime.Now.Year;
        if (!godine.Contains(tekucaGodina))
        {
            godine.Insert(0, tekucaGodina);
            godine = godine.OrderByDescending(g => g).ToList();
        }

        Godine = new ObservableCollection<int>(godine);

        var pocetnaGodina = AppConfig.ActiveGodina.HasValue && Godine.Contains(AppConfig.ActiveGodina.Value)
            ? AppConfig.ActiveGodina.Value
            : (Godine.Contains(tekucaGodina) ? tekucaGodina : Godine.FirstOrDefault());

        SelectedGodina = pocetnaGodina;
    }

    private void UcitajMesecniPregled(int godina)
    {
        var obracuniGodine = _db.ObracuniPlata
            .Where(o => o.Godina == godina)
            .Select(o => new { o.Mesec, o.NetoIsplata, o.PorezNaDohodak, o.DoprinosPioRadnik, o.DoprinosZdravstvoRadnik, o.DoprinosNezaposlenostRadnik, o.KreditObustava, o.Samodoprinosi, o.DoprinosPioPoslodavac, o.DoprinosZdravstvoPoslodavac, o.DoprinosNezaposlenostPoslodavac })
            .ToList();

        var brojRadnikaGodine = _db.Radnici
            .Where(r => r.Godina == godina && r.Aktivan)
            .GroupBy(r => r.Mesec)
            .Select(g => new { Mesec = g.Key, Broj = g.Count() })
            .ToList();

        var netoPoMesecu = new double[12];
        var brutoPoMesecu = new double[12];
        var brojPoMesecu = new double[12];

        foreach (var grupa in obracuniGodine.GroupBy(o => o.Mesec))
        {
            if (grupa.Key < 1 || grupa.Key > 12) continue;
            var neto = grupa.Sum(o => o.NetoIsplata);
            var bruto2 = grupa.Sum(o => o.NetoIsplata + o.PorezNaDohodak + o.DoprinosPioRadnik + o.DoprinosZdravstvoRadnik + o.DoprinosNezaposlenostRadnik
                + o.KreditObustava + o.Samodoprinosi + o.DoprinosPioPoslodavac + o.DoprinosZdravstvoPoslodavac + o.DoprinosNezaposlenostPoslodavac);
            netoPoMesecu[grupa.Key - 1] = (double)neto;
            brutoPoMesecu[grupa.Key - 1] = (double)bruto2;
        }

        foreach (var stavka in brojRadnikaGodine)
        {
            if (stavka.Mesec is >= 1 and <= 12)
            {
                brojPoMesecu[stavka.Mesec - 1] = stavka.Broj;
            }
        }

        MesecniSeries = new ISeries[]
        {
            new ColumnSeries<double>
            {
                Values = netoPoMesecu,
                Name = "Neto masa",
                Fill = new SolidColorPaint(SKColor.Parse("#10B981")),
                DataLabelsPaint = new SolidColorPaint(SKColor.Parse("#333333")),
                DataLabelsPosition = DataLabelsPosition.Top,
                DataLabelsFormatter = point => FormatKratakBroj(point.Model),
                YToolTipLabelFormatter = point => $"{point.Model:N2}",
                ScalesYAt = 0
            },
            new ColumnSeries<double>
            {
                Values = brutoPoMesecu,
                Name = "Bruto masa",
                Fill = new SolidColorPaint(SKColor.Parse("#4C1D95")),
                DataLabelsPaint = new SolidColorPaint(SKColor.Parse("#333333")),
                DataLabelsPosition = DataLabelsPosition.Top,
                DataLabelsFormatter = point => FormatKratakBroj(point.Model),
                YToolTipLabelFormatter = point => $"{point.Model:N2}",
                ScalesYAt = 0
            },
            new LineSeries<double>
            {
                Values = brojPoMesecu,
                Name = "Aktivnih radnika",
                Stroke = new SolidColorPaint(SKColor.Parse("#F59E0B")) { StrokeThickness = 3 },
                Fill = null,
                GeometryFill = new SolidColorPaint(SKColor.Parse("#F59E0B")),
                GeometryStroke = new SolidColorPaint(SKColor.Parse("#F59E0B")),
                GeometrySize = 8,
                YToolTipLabelFormatter = point => $"{point.Model:N0}",
                ScalesYAt = 1
            }
        };

        MesecniXAxes = new Axis[]
        {
            new Axis
            {
                Labels = NazivMeseca,
                TextSize = 12
            }
        };

        MesecniYAxes = new Axis[]
        {
            new Axis
            {
                Name = "Neto / Bruto masa",
                Position = AxisPosition.Start,
                TextSize = 11
            },
            new Axis
            {
                Name = "Broj radnika",
                Position = AxisPosition.End,
                TextSize = 11,
                SeparatorsPaint = null
            }
        };
    }

    private static string FormatKratakBroj(double value)
    {
        var apsolutno = Math.Abs(value);
        if (apsolutno >= 1_000_000)
            return (value / 1_000_000).ToString("N2") + "M";
        if (apsolutno >= 1_000)
            return (value / 1_000).ToString("N1") + "K";
        return value.ToString("N0");
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
