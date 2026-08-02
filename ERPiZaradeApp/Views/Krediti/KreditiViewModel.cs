using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.EntityFrameworkCore;
using ERPiZaradeData;
using ERPiZaradeData.Models;

namespace ERPiZaradeApp.Views.Krediti;

public class KreditiViewModel : INotifyPropertyChanged
{
    private readonly PlataDbContext _db;
    
    private ObservableCollection<Radnik> _allRadnici = [];
    private ObservableCollection<Radnik> _filteredRadnici = [];
    private Radnik? _selectedRadnik;
    
    private ObservableCollection<Kredit> _krediti = [];
    
    private string _searchText = "";
    private string _statusText = "Spreman za rad";
    
    private decimal _totalOutstandingDebt;
    private int _activeCreditsCount;
    
    public event PropertyChangedEventHandler? PropertyChanged;

    public KreditiViewModel()
    {
        _db = PlataDbContext.Create(AppConfig.DbPath);
        _ = LoadRadniciAsync();
    }

    public async Task LoadRadniciAsync()
    {
        try
        {
            StatusText = "Učitavanje zaposlenih...";
            var radniciList = await _db.Radnici
                .Where(r => r.Aktivan)
                .OrderBy(r => r.BrojRadnika)
                .ToListAsync();

            AllRadnici = new ObservableCollection<Radnik>(radniciList);
            ApplyFilter();
            
            if (FilteredRadnici.Count > 0)
            {
                SelectedRadnik = FilteredRadnici.FirstOrDefault();
            }
            StatusText = "Spreman za rad";
        }
        catch (Exception ex)
        {
            StatusText = $"Greška pri učitavanju: {ex.Message}";
        }
    }

    private void ApplyFilter()
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            FilteredRadnici = new ObservableCollection<Radnik>(AllRadnici);
        }
        else
        {
            var text = SearchText.ToLower();
            var filtered = AllRadnici.Where(r => 
                r.ImeIPrezime.ToLower().Contains(text) || 
                r.BrojRadnika.ToString().Contains(text));
            FilteredRadnici = new ObservableCollection<Radnik>(filtered);
        }
    }

    public async Task LoadKreditiForSelectedRadnikAsync()
    {
        if (SelectedRadnik == null)
        {
            Krediti.Clear();
            TotalOutstandingDebt = 0;
            ActiveCreditsCount = 0;
            return;
        }

        try
        {
            StatusText = $"Učitavanje kredita za radnika: {SelectedRadnik.ImeIPrezime}...";
            
            var kreditiList = await _db.Krediti
                .Where(k => k.RadnikId == SelectedRadnik.Id)
                .OrderByDescending(k => k.Aktivan)
                .ThenByDescending(k => k.DatumPocetka)
                .ToListAsync();

            Krediti = new ObservableCollection<Kredit>(kreditiList);
            
            TotalOutstandingDebt = kreditiList.Sum(k => k.OstatakDuga);
            ActiveCreditsCount = kreditiList.Count(k => k.Aktivan);
            
            StatusText = "Spreman za rad";
        }
        catch (Exception ex)
        {
            StatusText = $"Greška pri učitavanju kredita: {ex.Message}";
        }
    }

    public async Task ToggleKreditAktivnostAsync(Kredit kredit)
    {
        try
        {
            kredit.Aktivan = !kredit.Aktivan;
            _db.Entry(kredit).State = EntityState.Modified;
            await _db.SaveChangesAsync();
            
            ActiveCreditsCount = Krediti.Count(k => k.Aktivan);
            StatusText = $"Status kredita '{kredit.Opis}' uspešno izmenjen.";
        }
        catch (Exception ex)
        {
            StatusText = $"Greška: {ex.Message}";
            MessageBox.Show($"Nije moguće sačuvati promenu aktivnosti: {ex.Message}", "Greška", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public async Task DeleteKreditAsync(Kredit kredit)
    {
        var result = MessageBox.Show(
            $"Da li ste sigurni da želite da obrišete kredit '{kredit.Opis}' za radnika {SelectedRadnik?.ImeIPrezime}?\nOvo će trajno ukloniti sve podatke o ovom dugu iz baze.",
            "Potvrda brisanja", MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            try
            {
                StatusText = "Brisanje kredita...";
                _db.Krediti.Remove(kredit);
                await _db.SaveChangesAsync();
                
                await LoadKreditiForSelectedRadnikAsync();
                StatusText = "Kredit uspešno obrisan.";
            }
            catch (Exception ex)
            {
                StatusText = $"Greška: {ex.Message}";
                MessageBox.Show($"Greška prilikom brisanja kredita: {ex.Message}", "Greška", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    public ObservableCollection<Radnik> AllRadnici
    {
        get => _allRadnici;
        set { _allRadnici = value; OnPropertyChanged(); }
    }

    public ObservableCollection<Radnik> FilteredRadnici
    {
        get => _filteredRadnici;
        set { _filteredRadnici = value; OnPropertyChanged(); }
    }

    public Radnik? SelectedRadnik
    {
        get => _selectedRadnik;
        set 
        { 
            _selectedRadnik = value; 
            OnPropertyChanged(); 
            _ = LoadKreditiForSelectedRadnikAsync();
        }
    }

    public ObservableCollection<Kredit> Krediti
    {
        get => _krediti;
        set { _krediti = value; OnPropertyChanged(); }
    }

    public string SearchText
    {
        get => _searchText;
        set 
        { 
            _searchText = value; 
            OnPropertyChanged(); 
            ApplyFilter(); 
        }
    }

    public string StatusText
    {
        get => _statusText;
        set { _statusText = value; OnPropertyChanged(); }
    }

    public decimal TotalOutstandingDebt
    {
        get => _totalOutstandingDebt;
        set { _totalOutstandingDebt = value; OnPropertyChanged(); }
    }

    public int ActiveCreditsCount
    {
        get => _activeCreditsCount;
        set { _activeCreditsCount = value; OnPropertyChanged(); }
    }

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
