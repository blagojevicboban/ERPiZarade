using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.EntityFrameworkCore;
using PlataData;
using PlataData.Models;

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

    public RadniciViewModel()
    {
        _db = PlataDbContext.Create(AppConfig.DbPath);
        LoadCommand = new RelayCommand(async _ => await LoadAsync());
        NewCommand = new RelayCommand(_ => NewRadnik(), _ => !IsEditing);
        SaveCommand = new RelayCommand(async _ => await SaveAsync(), _ => IsEditing && EditingRadnik != null);
        CancelCommand = new RelayCommand(_ => CancelEdit(), _ => IsEditing);
        DeleteCommand = new RelayCommand(async _ => await DeleteAsync(), _ => SelectedRadnik != null && !IsEditing);

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
                EditingRadnik = CopyRadnik(value);
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

    public ICommand LoadCommand { get; }
    public ICommand NewCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand DeleteCommand { get; }

    private async Task LoadAsync()
    {
        try
        {
            var query = _db.Radnici.AsQueryable();
            if (PrikazujeSamoAktivne)
                query = query.Where(r => r.Aktivan);
            if (!string.IsNullOrWhiteSpace(SearchText))
                query = query.Where(r => r.ImeIPrezime.Contains(SearchText) ||
                                         r.BrojRadnika.ToString().Contains(SearchText) ||
                                         r.MaticniBroj.Contains(SearchText));

            var list = await query.OrderBy(r => r.BrojRadnika).ToListAsync();
            Radnici = new ObservableCollection<Radnik>(list);
            StatusPoruka = $"Prikazano: {list.Count} radnika";
        }
        catch (Exception ex)
        {
            StatusPoruka = $"Greška pri učitavanju: {ex.Message}";
        }
    }

    private void NewRadnik()
    {
        EditingRadnik = new Radnik { Aktivan = true, BrojRadneJedinice = 1 };
        SelectedRadnik = null;
        IsEditing = true;
        StatusPoruka = "Unos novog radnika...";
    }

    private async Task SaveAsync()
    {
        if (EditingRadnik == null) return;
        try
        {
            if (EditingRadnik.Id == 0)
            {
                int nextId = (_db.Radnici.Select(r => (int?)r.Id).Max() ?? 0) + 1;
                EditingRadnik.Id = nextId;
                EditingRadnik.BrojRadnika = nextId;

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
            StatusPoruka = $"Greška pri čuvanju: {ex.Message}";
        }
    }

    private void CancelEdit()
    {
        IsEditing = false;
        EditingRadnik = SelectedRadnik != null ? CopyRadnik(SelectedRadnik) : null;
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
        Id = src.Id, ImeIPrezime = src.ImeIPrezime, Jmbg = src.Jmbg,
        BrojRadnika = src.BrojRadnika, MaticniBroj = src.MaticniBroj,
        Koeficijent = src.Koeficijent, OsnovnaPlata = src.OsnovnaPlata,
        BankovniRacun = src.BankovniRacun, NazivBanke = src.NazivBanke,
        Radno_Mesto = src.Radno_Mesto, BrojRadneJedinice = src.BrojRadneJedinice,
        Kategorija = src.Kategorija, Aktivan = src.Aktivan,
        LicnoOslobodjenje = src.LicnoOslobodjenje,
    };

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
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
