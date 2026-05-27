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
        set { _selectedObracun = value; OnPropertyChanged(); }
    }

    public string StatusText
    {
        get => _statusText;
        set { _statusText = value; OnPropertyChanged(); }
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

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
