using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PlataData;

namespace PlataApp.Views.Stampe;

public class StampeViewModel : INotifyPropertyChanged
{
    private readonly PlataDbContext _db;
    private ObservableCollection<int> _godine = [];
    private ObservableCollection<int> _meseci = [];
    private ObservableCollection<string> _radneJedinice = [];
    private int _selectedGodina;
    private int _selectedMesec;
    private string _selectedRadnaJedinica = "Sve radne jedinice";
    private string _statusText = "Spreman za rad";

    public event PropertyChangedEventHandler? PropertyChanged;

    public StampeViewModel()
    {
        _db = PlataDbContext.Create(AppConfig.DbPath);

        // Inicijalizuj mesece
        Meseci = new ObservableCollection<int>(Enumerable.Range(1, 12));
        SelectedMesec = DateTime.Now.Month;

        // Inicijalizuj fiksne radne jedinice spram legacy sistema (1 do 9) i opciju "Sve"
        RadneJedinice = new ObservableCollection<string>
        {
            "Sve radne jedinice",
            "Radna jedinica 1",
            "Radna jedinica 2",
            "Radna jedinica 3",
            "Radna jedinica 4",
            "Radna jedinica 5",
            "Radna jedinica 6",
            "Radna jedinica 7",
            "Radna jedinica 8",
            "Radna jedinica 9"
        };
        SelectedRadnaJedinica = "Sve radne jedinice";

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
            {
                god = [DateTime.Now.Year];
            }

            Godine = new ObservableCollection<int>(god);
            SelectedGodina = Godine.FirstOrDefault();
        }
        catch (Exception ex)
        {
            StatusText = $"Greška pri inicijalizaciji: {ex.Message}";
        }
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

    public ObservableCollection<string> RadneJedinice
    {
        get => _radneJedinice;
        set { _radneJedinice = value; OnPropertyChanged(); }
    }

    public int SelectedGodina
    {
        get => _selectedGodina;
        set { _selectedGodina = value; OnPropertyChanged(); }
    }

    public int SelectedMesec
    {
        get => _selectedMesec;
        set { _selectedMesec = value; OnPropertyChanged(); }
    }

    public string SelectedRadnaJedinica
    {
        get => _selectedRadnaJedinica;
        set { _selectedRadnaJedinica = value; OnPropertyChanged(); }
    }

    public string StatusText
    {
        get => _statusText;
        set { _statusText = value; OnPropertyChanged(); }
    }

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
