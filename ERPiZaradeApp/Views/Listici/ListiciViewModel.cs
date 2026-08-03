using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.EntityFrameworkCore;
using ERPiZaradeData;
using ERPiZaradeData.Models;
using ERPiZaradeApp.Views.Radnici; // za RelayCommand

namespace ERPiZaradeApp.Views.Listici;

public class ObracunSelektivni : INotifyPropertyChanged
{
    private bool _isSelected = true; // defaultno selektovano radi bržeg masovnog izvoza

    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; OnPropertyChanged(); }
    }

    public ObracunPlate Obracun { get; set; } = null!;

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public class ListiciViewModel : INotifyPropertyChanged
{
    private readonly PlataDbContext _db;
    private ObservableCollection<ObracunSelektivni> _obracuni = [];
    private ObservableCollection<int> _godine = [];
    private ObservableCollection<int> _meseci = [];
    private int _selectedGodina;
    private int _selectedMesec;
    private string _searchText = "";
    private string _statusText = "";
    private bool _areAllSelected = true;

    public ListiciViewModel()
    {
        _db = PlataDbContext.Create(AppConfig.DbPath);

        LoadCommand = new RelayCommand(async _ => await LoadObracuneAsync());
        ClearFilterCommand = new RelayCommand(async _ => 
        {
            SearchText = "";
            await LoadObracuneAsync();
        });
        ToggleSelectAllCommand = new RelayCommand(_ => ToggleSelectAll());

        Meseci = new ObservableCollection<int>(Enumerable.Range(1, 12));
        SelectedMesec = AppConfig.ActiveMesec ?? DateTime.Now.Month;

        _ = InitAsync();
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

            if (AppConfig.ActiveGodina.HasValue && AppConfig.ActiveMesec.HasValue)
            {
                SelectedGodina = AppConfig.ActiveGodina.Value;
                SelectedMesec = AppConfig.ActiveMesec.Value;
            }
            else
            {
                SelectedGodina = Godine.FirstOrDefault();

                // Ako imamo aktivni mesec iz baze koji je pre Aprila 2026, postavi to
                var latestPeriod = await _db.ObracuniPlata
                    .OrderByDescending(o => o.Godina).ThenByDescending(o => o.Mesec)
                    .FirstOrDefaultAsync();

                if (latestPeriod != null)
                {
                    SelectedGodina = latestPeriod.Godina;
                    SelectedMesec = latestPeriod.Mesec;
                }
            }

            await LoadObracuneAsync();
        }
        catch (Exception ex)
        {
            StatusText = $"Greška prilikom inicijalizacije: {ex.Message}";
        }
    }

    public ObservableCollection<ObracunSelektivni> Obracuni
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

    public string StatusText
    {
        get => _statusText;
        set { _statusText = value; OnPropertyChanged(); }
    }

    public ICommand LoadCommand { get; }
    public ICommand ClearFilterCommand { get; }
    public ICommand ToggleSelectAllCommand { get; }

    public async Task LoadObracuneAsync()
    {
        try
        {
            StatusText = "Učitavanje obračuna...";
            // Stornirani obračun nije isplaćen — listić po njemu bi radniku pokazao platu
            // koju nije primio.
            var query = _db.ObracuniPlata
                .Include(o => o.Radnik)
                .Where(o => o.Godina == SelectedGodina && o.Mesec == SelectedMesec && !o.Storniran);

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var lowerSearch = SearchText.ToLower();
                query = query.Where(o => o.Radnik.ImeIPrezime.ToLower().Contains(lowerSearch));
            }

            var rawList = await query.OrderBy(o => o.Radnik.BrojRadnika).ToListAsync();
            
            var list = rawList.Select(o => new ObracunSelektivni 
            { 
                Obracun = o, 
                IsSelected = _areAllSelected 
            }).ToList();

            Obracuni = new ObservableCollection<ObracunSelektivni>(list);
            StatusText = $"Pronađeno: {list.Count} obračuna za {SelectedMesec}.{SelectedGodina}.";
        }
        catch (Exception ex)
        {
            StatusText = $"Greška: {ex.Message}";
        }
    }

    private void ToggleSelectAll()
    {
        _areAllSelected = !_areAllSelected;
        foreach (var item in Obracuni)
        {
            item.IsSelected = _areAllSelected;
        }
        StatusText = _areAllSelected ? "Izabrani svi zaposleni." : "Deselektovani svi zaposleni.";
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
