using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.EntityFrameworkCore;
using PlataData;
using PlataData.Models;
using PlataApp.Views.Radnici; // Za RelayCommand
using PlataApp.Services;

namespace PlataApp.Views.PppPd;

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
        
        LoadCommand = new RelayCommand(async _ => await LoadObracuneAsync());
        ValidateCommand = new RelayCommand(_ => ValidateData());
        
        Meseci = new ObservableCollection<int>(Enumerable.Range(1, 12));
        SelectedMesec = AppConfig.ActiveMesec ?? DateTime.Now.Month;
        
        // Default klijentska oznaka
        KlijentskaOznaka = $"DECL-{DateTime.Now:ddMMyyyy}";
        
        UcitajFirmaPodatke();

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

            var list = await _db.ObracuniPlata
                .Include(o => o.Radnik)
                .Where(o => o.Godina == SelectedGodina && o.Mesec == SelectedMesec)
                .OrderBy(o => o.Radnik.BrojRadnika)
                .ToListAsync();

            Obracuni = new ObservableCollection<ObracunPlate>(list);
            
            // Izračunaj zbirne KPI vrednosti
            TotalRadnika = list.Count;
            UkupnoBruto = list.Sum(o => o.BrutoZarada + o.BrutoBolovanje);
            UkupnoPorez = list.Sum(o => o.PorezNaDohodak);
            
            // Ukupni doprinosi (PIO + ZDR + NEZ) i za radnika i za poslodavca
            UkupnoDoprinosi = list.Sum(o => 
                o.DoprinosPioRadnik + o.DoprinosZdravstvoRadnik + o.DoprinosNezaposlenostRadnik +
                o.DoprinosPioPoslodavac + o.DoprinosZdravstvoPoslodavac + o.DoprinosNezaposlenostPoslodavac);

            StatusText = $"Učitano {list.Count} obračuna za {SelectedMesec:D2}.{SelectedGodina}.";
            
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
            _ = LoadObracuneAsync(); 
        }
    }

    public ObservableCollection<ObracunPlate> Obracuni
    {
        get => _obracuni;
        set { _obracuni = value; OnPropertyChanged(); }
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

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
