using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.EntityFrameworkCore;
using PlataData;
using PlataData.Models;
using PlataApp.Views.Radnici; // za RelayCommand

namespace PlataApp.Views.Obracun;

public class ObracunViewModel : INotifyPropertyChanged
{
    private readonly PlataDbContext _db;
    private ObservableCollection<ObracunPlate> _obracuni = [];
    private ObservableCollection<int> _godine = [];
    private ObservableCollection<int> _meseci = [];
    private int _selectedGodina;
    private int _selectedMesec;
    private string _searchText = "";
    private ObracunPlate? _selectedObracun;
    private string _statusText = "";
    private decimal _selectedVrBoda;
    private decimal _selectedMinuliRadPercent;
    private decimal _selectedStimulacijaPercent;

    public ObracunViewModel()
    {
        _db = PlataDbContext.Create(AppConfig.DbPath);
        
        LoadCommand = new RelayCommand(async _ => await LoadObracuneAsync());
        ClearFilterCommand = new RelayCommand(async _ => 
        {
            SearchText = "";
            await LoadObracuneAsync();
        });
        
        // Inicijalizuj mesece
        Meseci = new ObservableCollection<int>(Enumerable.Range(1, 12));
        SelectedMesec = AppConfig.ActiveMesec ?? DateTime.Now.Month;

        _ = InitAsync();
    }

    private async Task InitAsync()
    {
        try
        {
            // Izvuci sve dostupne godine iz baze
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
            StatusText = $"Greška prilikom inicijalizacije: {ex.Message}";
        }
    }

    public ObservableCollection<ObracunPlate> Obracuni
    {
        get => _obracuni;
        set { _obracuni = value; OnPropertyChanged(); }
    }

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
        }
    }

    public string SearchText
    {
        get => _searchText;
        set 
        { 
            _searchText = value; 
            OnPropertyChanged(); 
            _ = LoadObracuneAsync();
        }
    }

    public ObracunPlate? SelectedObracun
    {
        get => _selectedObracun;
        set 
        { 
            _selectedObracun = value; 
            OnPropertyChanged(); 
            if (_selectedObracun != null)
            {
                _ = UcitajStopeZaObracunAsync(_selectedObracun);
            }
        }
    }

    public string StatusText
    {
        get => _statusText;
        set { _statusText = value; OnPropertyChanged(); }
    }
    
    public decimal SelectedVrBoda
    {
        get => _selectedVrBoda;
        set { _selectedVrBoda = value; OnPropertyChanged(); }
    }

    public decimal SelectedMinuliRadPercent
    {
        get => _selectedMinuliRadPercent;
        set { _selectedMinuliRadPercent = value; OnPropertyChanged(); }
    }

    public decimal SelectedStimulacijaPercent
    {
        get => _selectedStimulacijaPercent;
        set { _selectedStimulacijaPercent = value; OnPropertyChanged(); }
    }

    public ICommand LoadCommand { get; }
    public ICommand ClearFilterCommand { get; }

    public async Task LoadObracuneAsync()
    {
        try
        {
            StatusText = "Učitavanje obračuna...";
            var query = _db.ObracuniPlata
                .Include(o => o.Radnik)
                .Where(o => o.Godina == SelectedGodina && o.Mesec == SelectedMesec);

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var lowerSearch = SearchText.ToLower();
                query = query.Where(o => o.Radnik.ImeIPrezime.ToLower().Contains(lowerSearch));
            }

            var list = await query.OrderBy(o => o.Radnik.BrojRadnika).ToListAsync();
            Obracuni = new ObservableCollection<ObracunPlate>(list);
            StatusText = $"Pronađeno: {list.Count} obračuna za {SelectedMesec}.{SelectedGodina}.";
        }
        catch (Exception ex)
        {
            StatusText = $"Greška: {ex.Message}";
        }
    }

    private async Task UcitajStopeZaObracunAsync(ObracunPlate o)
    {
        try
        {
            // Učitaj vrednost boda za tu godinu i mesec iz baze podataka
            var porez = await _db.Porezi
                .FirstOrDefaultAsync(p => p.Godina == o.Godina && p.Mesec == o.Mesec);
            SelectedVrBoda = porez?.VrBoda ?? 1860.34m;

            decimal procMinul = porez?.ProcMinul ?? 0.40m;
            SelectedMinuliRadPercent = o.MinuliRadGodine * procMinul;

            // Učitaj stimulaciju za tog radnika, godinu i mesec
            var radniSati = await _db.RadniSati
                .FirstOrDefaultAsync(r => r.RadnikId == o.RadnikId && r.Godina == o.Godina && r.Mesec == o.Mesec);
            SelectedStimulacijaPercent = radniSati?.Stimulacija ?? 0m;

            // Učitaj doprinose za tu godinu i mesec iz baze podataka
            var stope = await _db.Doprinosi
                .Where(d => d.Godina == o.Godina && d.Mesec == o.Mesec)
                .ToListAsync();

            // Ako nema u bazi za taj mesec, probaj najbliži prethodni
            if (!stope.Any())
            {
                var closest = await _db.Doprinosi
                    .Where(d => d.Godina < o.Godina || (d.Godina == o.Godina && d.Mesec < o.Mesec))
                    .OrderByDescending(d => d.Godina)
                    .ThenByDescending(d => d.Mesec)
                    .FirstOrDefaultAsync();

                if (closest != null)
                {
                    stope = await _db.Doprinosi
                        .Where(d => d.Godina == closest.Godina && d.Mesec == closest.Mesec)
                        .ToListAsync();
                }
            }

            // Podrazumevane stope za radnika (Srbija)
            decimal empPio = 14.00m;
            decimal empZdr = 5.15m;
            decimal empNez = 0.75m;

            // Podrazumevane stope za poslodavca (Srbija)
            decimal bossPio = 10.00m;
            decimal bossZdr = 5.15m;
            decimal bossNez = 0.00m;

            // Fallback stope poslodavca na osnovu perioda (ako su u bazi 0)
            if (o.Godina >= 2023) { bossPio = 10.00m; bossNez = 0.00m; }
            else if (o.Godina == 2022) { bossPio = 11.00m; bossNez = 0.00m; }
            else if (o.Godina >= 2020 || (o.Godina == 2019 && o.Mesec == 12)) { bossPio = 11.50m; bossNez = 0.00m; }
            else { bossPio = 12.00m; bossNez = 0.75m; }

            if (stope.Any())
            {
                var pioRec = stope.FirstOrDefault(d => d.RedniBroj == 1);
                if (pioRec != null)
                {
                    empPio = pioRec.ProcRadn;
                    if (pioRec.ProcPosl > 0) bossPio = pioRec.ProcPosl;
                }

                var zdrRec = stope.FirstOrDefault(d => d.RedniBroj == 2);
                if (zdrRec != null)
                {
                    empZdr = zdrRec.ProcRadn;
                    if (zdrRec.ProcPosl > 0) bossZdr = zdrRec.ProcPosl;
                }

                var nezRec = stope.FirstOrDefault(d => d.RedniBroj == 3);
                if (nezRec != null)
                {
                    empNez = nezRec.ProcRadn;
                    if (nezRec.ProcPosl > 0) bossNez = nezRec.ProcPosl;
                }
            }

            o.StopaPioRadnikStr = $"{empPio:F2}%";
            o.StopaZdravstvoRadnikStr = $"{empZdr:F2}%";
            o.StopaNezaposlenostRadnikStr = $"{empNez:F2}%";

            o.StopaPioPoslodavacStr = $"{bossPio:F2}%";
            o.StopaZdravstvoPoslodavacStr = $"{bossZdr:F2}%";
            o.StopaNezaposlenostPoslodavacStr = $"{bossNez:F2}%";

            // Obavesti UI da se promenio SelectedObracun kako bi ponovo procitao sve NotMapped string propertije
            OnPropertyChanged(nameof(SelectedObracun));
        }
        catch { }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
