using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using PlataData;
using PlataData.Models;

namespace PlataApp.Views.Podesavanja;

public partial class PodesavanjaPage : Page
{
    private readonly PlataDbContext _db;
    private Porezi? _currentParams;
    private List<Doprinos> _currentDoprinosi = new();
    private Doprinos? _selectedDoprinos;
    private Firma? _currentFirma;
    private PlatniRazred? _currentRazredi;

    public PodesavanjaPage()
    {
        InitializeComponent();
        _db = PlataDbContext.Create(AppConfig.DbPath);

        InicijalizujPeriodSelectore();
        UcitajTrenutniPeriod();
        UcitajFirmaPodatke();
        UcitajPlatneRazrede();
    }

    private void InicijalizujPeriodSelectore()
    {
        // Godine: od 2000 do 2030 + bilo koje godine iz baze
        var years = Enumerable.Range(2000, 31).ToList();
        try
        {
            var dbYears = _db.Porezi.Select(p => p.Godina).Distinct().ToList();
            foreach (var y in dbYears)
            {
                if (!years.Contains(y))
                    years.Add(y);
            }
        }
        catch { }

        ComboGodina.ItemsSource = years.OrderByDescending(y => y).ToList();
        ComboMesec.ItemsSource = Enumerable.Range(1, 12).ToList();
    }

    private void UcitajTrenutniPeriod()
    {
        try
        {
            // Podrazumevani period je poslednji obračunat period
            var latestObracun = _db.ObracuniPlata
                .OrderByDescending(o => o.Godina)
                .ThenByDescending(o => o.Mesec)
                .FirstOrDefault();

            int defGodina = latestObracun?.Godina ?? DateTime.Now.Year;
            int defMesec = latestObracun?.Mesec ?? DateTime.Now.Month;

            ComboGodina.SelectedItem = defGodina;
            ComboMesec.SelectedItem = defMesec;

            UcitajParametre(defGodina, defMesec);
        }
        catch (Exception ex)
        {
            StatusMessage.Text = $"Greška pri inicijalizaciji: {ex.Message}";
        }
    }

    private void UcitajParametre(int godina, int mesec)
    {
        try
        {
            // 1. Učitavanje poreza i opštih parametara
            _currentParams = _db.Porezi
                .FirstOrDefault(p => p.Godina == godina && p.Mesec == mesec);

            bool poreskiNisuPostojali = false;
            if (_currentParams == null)
            {
                poreskiNisuPostojali = true;
                // Fallback: najbliži prethodni mesec
                var fallback = _db.Porezi
                    .Where(p => p.Godina < godina || (p.Godina == godina && p.Mesec < mesec))
                    .OrderByDescending(p => p.Godina)
                    .ThenByDescending(p => p.Mesec)
                    .FirstOrDefault();

                if (fallback != null)
                {
                    _currentParams = new Porezi
                    {
                        Godina = godina,
                        Mesec = mesec,
                        RedniBroj = 1,
                        Zarada = fallback.Zarada,
                        AkPorez = fallback.AkPorez,
                        AkPorez2 = fallback.AkPorez2,
                        AkPorez3 = fallback.AkPorez3,
                        AkPorez4 = fallback.AkPorez4,
                        Prvast = fallback.Prvast,
                        Drugast = fallback.Drugast,
                        Trecast = fallback.Trecast,
                        LinPorez3 = fallback.LinPorez3,
                        SifPlac1 = fallback.SifPlac1,
                        ZiroR1 = fallback.ZiroR1,
                        PozivNa1 = fallback.PozivNa1,
                        PozivNa3 = fallback.PozivNa3,
                        Svrha1 = fallback.Svrha1,
                        Svrha2 = fallback.Svrha2,
                        Primalac1 = fallback.Primalac1,
                        Primalac2 = fallback.Primalac2,
                        SifPlac2 = fallback.SifPlac2,
                        ZiroR2 = fallback.ZiroR2,
                        PozivNa2 = fallback.PozivNa2,
                        PozivNa4 = fallback.PozivNa4,
                        PosPorez = fallback.PosPorez,
                        Svrha3 = fallback.Svrha3,
                        Svrha4 = fallback.Svrha4,
                        Primalac3 = fallback.Primalac3,
                        Primalac4 = fallback.Primalac4,
                        ProcDrzav = fallback.ProcDrzav,
                        ProcNocni = fallback.ProcNocni,
                        ProcPreko = fallback.ProcPreko,
                        ProcMinul = fallback.ProcMinul,
                        ProcNedel = fallback.ProcNedel,
                        ProcBolov = fallback.ProcBolov,
                        ProcPlac = fallback.ProcPlac,
                        ProcPlZa = fallback.ProcPlZa,
                        ProcInval = fallback.ProcInval,
                        FondCasova = fallback.FondCasova,
                        CasZaOb = fallback.CasZaOb,
                        VrBoda = fallback.VrBoda,
                        ProcIzdrz = fallback.ProcIzdrz,
                        Akont = fallback.Akont,
                        ProsBrut = fallback.ProsBrut
                    };
                }
                else
                {
                    _currentParams = new Porezi
                    {
                        Godina = godina,
                        Mesec = mesec,
                        RedniBroj = 1,
                        Zarada = 45950.00m,
                        AkPorez = 10.00m,
                        Prvast = 28423.00m,
                        Drugast = 656425.00m,
                        ProcDrzav = 110.00m,
                        ProcNocni = 26.00m,
                        ProcPreko = 26.00m,
                        ProcMinul = 0.40m,
                        ProcNedel = 10.00m,
                        ProcBolov = 65.00m,
                        ProcPlac = 65.00m,
                        ProcPlZa = 100.00m,
                        ProcInval = 85.00m,
                        FondCasova = 176,
                        CasZaOb = 176,
                        VrBoda = 1860.34m,
                        Akont = "DA",
                        SifPlac1 = "254",
                        SifPlac2 = "254",
                        Primalac1 = "PORESKA UPRAVA"
                    };
                }
            }

            PopuniFormu();

            // 2. Učitavanje doprinosa za isti period
            _currentDoprinosi = _db.Doprinosi
                .Where(d => d.Godina == godina && d.Mesec == mesec)
                .OrderBy(d => d.RedniBroj)
                .ToList();

            bool doprinosiNisuPostojali = false;
            if (!_currentDoprinosi.Any())
            {
                doprinosiNisuPostojali = true;
                // Fallback na najbliži prethodni mesec iz baze
                var fallbackPeriod = _db.Doprinosi
                    .Where(d => d.Godina < godina || (d.Godina == godina && d.Mesec < mesec))
                    .OrderByDescending(d => d.Godina)
                    .ThenByDescending(d => d.Mesec)
                    .FirstOrDefault();

                if (fallbackPeriod != null)
                {
                    var fallbackList = _db.Doprinosi
                        .Where(d => d.Godina == fallbackPeriod.Godina && d.Mesec == fallbackPeriod.Mesec)
                        .OrderBy(d => d.RedniBroj)
                        .ToList();

                    _currentDoprinosi = fallbackList.Select(d => new Doprinos
                    {
                        Godina = godina,
                        Mesec = mesec,
                        RedniBroj = d.RedniBroj,
                        Naziv = d.Naziv,
                        ProcRadn = d.ProcRadn,
                        ProcPosl = d.ProcPosl,
                        B60ProcR = d.B60ProcR,
                        B60ProcP = d.B60ProcP,
                        Bp60ProcP = d.Bp60ProcP,
                        Bp60FProcP = d.Bp60FProcP,
                        PorProcP = d.PorProcP,
                        NepProcP = d.NepProcP,
                        InvProcP = d.InvProcP,
                        Svrha1 = d.Svrha1,
                        Svrha2 = d.Svrha2,
                        Primalac1 = d.Primalac1,
                        Primalac2 = d.Primalac2,
                        ZiroRacun = d.ZiroRacun,
                        ZiroRacP = d.ZiroRacP,
                        PozivNaB = d.PozivNaB,
                        PozivNa2 = d.PozivNa2,
                        SifPlac = d.SifPlac,
                        SifPlacP = d.SifPlacP
                    }).ToList();
                }
                else
                {
                    // Defoltne stope ako nema ničega u bazi (redovni defolti za PIO, Zdravstvo, Nezaposlenost)
                    _currentDoprinosi = new List<Doprinos>
                    {
                        new Doprinos
                        {
                            Godina = godina,
                            Mesec = mesec,
                            RedniBroj = 1,
                            Naziv = "PENZIJSKO-INVALIDSKO (PIO)",
                            ProcRadn = 14.00m,
                            ProcPosl = 10.00m,
                            SifPlac = "254",
                            SifPlacP = "254",
                            Primalac1 = "BUDŽET REPUBLIKE SRBIJE"
                        },
                        new Doprinos
                        {
                            Godina = godina,
                            Mesec = mesec,
                            RedniBroj = 2,
                            Naziv = "ZDRAVSTVENO OSIGURANJE",
                            ProcRadn = 5.15m,
                            ProcPosl = 5.15m,
                            SifPlac = "254",
                            SifPlacP = "254",
                            Primalac1 = "BUDŽET REPUBLIKE SRBIJE"
                        },
                        new Doprinos
                        {
                            Godina = godina,
                            Mesec = mesec,
                            RedniBroj = 3,
                            Naziv = "NEZAPOSLENOST",
                            ProcRadn = 0.75m,
                            ProcPosl = 0.00m,
                            SifPlac = "254",
                            SifPlacP = "254",
                            Primalac1 = "BUDŽET REPUBLIKE SRBIJE"
                        }
                    };
                }
            }

            // Povezivanje na DataGrid
            DoprinosiGrid.ItemsSource = null;
            DoprinosiGrid.ItemsSource = _currentDoprinosi;

            // Selektovanje prvog elementa
            if (_currentDoprinosi.Any())
            {
                DoprinosiGrid.SelectedItem = _currentDoprinosi.First();
            }
            else
            {
                ClearDoprinosiForm();
            }

            // Postavljanje status poruke
            if (poreskiNisuPostojali || doprinosiNisuPostojali)
            {
                StatusMessage.Text = $"Parametri za {mesec}.{godina} nisu postojali u bazi. Kopirani su iz prethodnog perioda ili kreirani na osnovu šablona.";
            }
            else
            {
                StatusMessage.Text = $"Uspešno učitani parametri i doprinosi za {mesec}.{godina}.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage.Text = $"Greška pri učitavanju parametara: {ex.Message}";
        }
    }

    private void PopuniFormu()
    {
        if (_currentParams == null) return;

        TxtZarada.Text = _currentParams.Zarada.ToString("N2");
        TxtVrBoda.Text = _currentParams.VrBoda.ToString("N4");
        TxtFondCasova.Text = _currentParams.FondCasova.ToString();
        TxtCasZaOb.Text = _currentParams.CasZaOb.ToString();
        ComboAkont.SelectedIndex = _currentParams.Akont.ToUpper() == "DA" ? 0 : 1;

        TxtAkPorez.Text = _currentParams.AkPorez.ToString("N2");
        TxtPrvast.Text = _currentParams.Prvast.ToString("N2");
        TxtDrugast.Text = _currentParams.Drugast.ToString("N2");
        TxtAkPorez2.Text = _currentParams.AkPorez2.ToString("N2");
        TxtAkPorez3.Text = _currentParams.AkPorez3.ToString("N2");

        TxtSifPlac1.Text = _currentParams.SifPlac1;
        TxtZiroR1.Text = _currentParams.ZiroR1;
        TxtPozivNa1.Text = _currentParams.PozivNa1;
        TxtPozivNa3.Text = _currentParams.PozivNa3;
        TxtSvrha1.Text = _currentParams.Svrha1;
        TxtPrimalac1.Text = _currentParams.Primalac1;

        TxtSifPlac2.Text = _currentParams.SifPlac2;
        TxtZiroR2.Text = _currentParams.ZiroR2;
        TxtPozivNa2.Text = _currentParams.PozivNa2;
        TxtPozivNa4.Text = _currentParams.PozivNa4;
        TxtSvrha3.Text = _currentParams.Svrha3;
        TxtPrimalac3.Text = _currentParams.Primalac3;

        TxtProcDrzav.Text = _currentParams.ProcDrzav.ToString("N2");
        TxtProcNocni.Text = _currentParams.ProcNocni.ToString("N2");
        TxtProcPreko.Text = _currentParams.ProcPreko.ToString("N2");
        TxtProcMinul.Text = _currentParams.ProcMinul.ToString("N2");
        TxtProcNedel.Text = _currentParams.ProcNedel.ToString("N2");
        TxtProcBolov.Text = _currentParams.ProcBolov.ToString("N2");
        TxtProcPlac.Text = _currentParams.ProcPlac.ToString("N2");
        TxtProcPlZa.Text = _currentParams.ProcPlZa.ToString("N2");
        TxtProcInval.Text = _currentParams.ProcInval.ToString("N2");
    }

    private void BtnUcitaj_Click(object sender, RoutedEventArgs e)
    {
        if (ComboGodina.SelectedItem is int godina && ComboMesec.SelectedItem is int mesec)
        {
            UcitajParametre(godina, mesec);
        }
    }

    private void BtnSacuvaj_Click(object sender, RoutedEventArgs e)
    {
        if (_currentParams == null) return;

        try
        {
            _currentParams.Zarada = ParseDecimal(TxtZarada.Text);
            _currentParams.VrBoda = ParseDecimal(TxtVrBoda.Text);
            _currentParams.FondCasova = ParseInt(TxtFondCasova.Text);
            _currentParams.CasZaOb = ParseInt(TxtCasZaOb.Text);
            _currentParams.Akont = ComboAkont.SelectedIndex == 0 ? "DA" : "NE";

            _currentParams.AkPorez = ParseDecimal(TxtAkPorez.Text);
            _currentParams.Prvast = ParseDecimal(TxtPrvast.Text);
            _currentParams.Drugast = ParseDecimal(TxtDrugast.Text);
            _currentParams.AkPorez2 = ParseDecimal(TxtAkPorez2.Text);
            _currentParams.AkPorez3 = ParseDecimal(TxtAkPorez3.Text);

            _currentParams.SifPlac1 = TxtSifPlac1.Text.Trim();
            _currentParams.ZiroR1 = TxtZiroR1.Text.Trim();
            _currentParams.PozivNa1 = TxtPozivNa1.Text.Trim();
            _currentParams.PozivNa3 = TxtPozivNa3.Text.Trim();
            _currentParams.Svrha1 = TxtSvrha1.Text.Trim();
            _currentParams.Primalac1 = TxtPrimalac1.Text.Trim();

            _currentParams.SifPlac2 = TxtSifPlac2.Text.Trim();
            _currentParams.ZiroR2 = TxtZiroR2.Text.Trim();
            _currentParams.PozivNa2 = TxtPozivNa2.Text.Trim();
            _currentParams.PozivNa4 = TxtPozivNa4.Text.Trim();
            _currentParams.Svrha3 = TxtSvrha3.Text.Trim();
            _currentParams.Primalac3 = TxtPrimalac3.Text.Trim();

            _currentParams.ProcDrzav = ParseDecimal(TxtProcDrzav.Text);
            _currentParams.ProcNocni = ParseDecimal(TxtProcNocni.Text);
            _currentParams.ProcPreko = ParseDecimal(TxtProcPreko.Text);
            _currentParams.ProcMinul = ParseDecimal(TxtProcMinul.Text);
            _currentParams.ProcNedel = ParseDecimal(TxtProcNedel.Text);
            _currentParams.ProcBolov = ParseDecimal(TxtProcBolov.Text);
            _currentParams.ProcPlac = ParseDecimal(TxtProcPlac.Text);
            _currentParams.ProcPlZa = ParseDecimal(TxtProcPlZa.Text);
            _currentParams.ProcInval = ParseDecimal(TxtProcInval.Text);

            // Provjera da li zapis već postoji u bazi
            var postojeci = _db.Porezi.Any(p => p.Id == _currentParams.Id || (p.Godina == _currentParams.Godina && p.Mesec == _currentParams.Mesec));
            if (!postojeci)
            {
                _db.Porezi.Add(_currentParams);
            }
            else
            {
                _db.Entry(_currentParams).State = Microsoft.EntityFrameworkCore.EntityState.Modified;
            }

            _db.SaveChanges();
            StatusMessage.Text = $"Opšti poreski parametri za period {_currentParams.Mesec}.{_currentParams.Godina} su uspešno sačuvani!";
            MessageBox.Show($"Poreski parametri za period {_currentParams.Mesec}.{_currentParams.Godina} su uspešno sačuvani!", "Uspeh", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Greška pri čuvanju poreskih podešavanja: {ex.Message}", "Greška", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ── DOPRINOSI LOGIKA EKRANA ──────────────────────────────────────

    private void DoprinosiGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DoprinosiGrid.SelectedItem is DoprunosiWrapSelected || DoprinosiGrid.SelectedItem is Doprinos d)
        {
            PopuniDoprinosiFormu((Doprinos)DoprinosiGrid.SelectedItem);
        }
        else
        {
            ClearDoprinosiForm();
        }
    }

    private void ClearDoprinosiForm()
    {
        _selectedDoprinos = null;
        TxtSelectedDoprinosTitle.Text = "📝 Uredi doprinos";
        TxtSelectedDoprinosSubtitle.Text = "Izaberite doprinos iz liste za izmenu stopa i računa";

        TxtDopNaziv.Text = "";
        TxtDopRedBroj.Text = "";
        TxtDopProcRadn.Text = "";
        TxtDopProcPosl.Text = "";
        TxtDopB60ProcR.Text = "";
        TxtDopB60ProcP.Text = "";
        TxtDopBp60ProcP.Text = "";
        TxtDopBp60FProcP.Text = "";
        TxtDopPorProcP.Text = "";
        TxtDopNepProcP.Text = "";
        TxtDopInvProcP.Text = "";
        TxtDopPrimalac1.Text = "";
        TxtDopPrimalac2.Text = "";
        TxtDopSvrha1.Text = "";
        TxtDopSvrha2.Text = "";
        TxtDopZiroRacun.Text = "";
        TxtDopPozivNaB.Text = "";
        TxtDopSifPlac.Text = "";
        TxtDopZiroRacP.Text = "";
        TxtDopPozivNa2.Text = "";
        TxtDopSifPlacP.Text = "";

        PanelDoprinosForm.IsEnabled = false;
        BtnSacuvajDoprinos.IsEnabled = false;
    }

    private void PopuniDoprinosiFormu(Doprinos d)
    {
        if (d == null) return;
        _selectedDoprinos = d;
        TxtSelectedDoprinosTitle.Text = $"📝 Uredi: {d.Naziv}";
        TxtSelectedDoprinosSubtitle.Text = $"Parametri za redni broj {d.RedniBroj} u periodu {d.Mesec}.{d.Godina}.";

        TxtDopNaziv.Text = d.Naziv;
        TxtDopRedBroj.Text = d.RedniBroj.ToString();
        TxtDopProcRadn.Text = d.ProcRadn.ToString("N2");
        TxtDopProcPosl.Text = d.ProcPosl.ToString("N2");
        TxtDopB60ProcR.Text = d.B60ProcR.ToString("N2");
        TxtDopB60ProcP.Text = d.B60ProcP.ToString("N2");
        TxtDopBp60ProcP.Text = d.Bp60ProcP.ToString("N2");
        TxtDopBp60FProcP.Text = d.Bp60FProcP.ToString("N2");
        TxtDopPorProcP.Text = d.PorProcP.ToString("N2");
        TxtDopNepProcP.Text = d.NepProcP.ToString("N2");
        TxtDopInvProcP.Text = d.InvProcP.ToString("N2");
        TxtDopPrimalac1.Text = d.Primalac1;
        TxtDopPrimalac2.Text = d.Primalac2;
        TxtDopSvrha1.Text = d.Svrha1;
        TxtDopSvrha2.Text = d.Svrha2;
        TxtDopZiroRacun.Text = d.ZiroRacun;
        TxtDopPozivNaB.Text = d.PozivNaB;
        TxtDopSifPlac.Text = d.SifPlac;
        TxtDopZiroRacP.Text = d.ZiroRacP;
        TxtDopPozivNa2.Text = d.PozivNa2;
        TxtDopSifPlacP.Text = d.SifPlacP;

        PanelDoprinosForm.IsEnabled = true;
        BtnSacuvajDoprinos.IsEnabled = true;
    }

    private void BtnSacuvajDoprinos_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedDoprinos == null) return;

        try
        {
            _selectedDoprinos.Naziv = TxtDopNaziv.Text.Trim();
            _selectedDoprinos.ProcRadn = ParseDecimal(TxtDopProcRadn.Text);
            _selectedDoprinos.ProcPosl = ParseDecimal(TxtDopProcPosl.Text);
            _selectedDoprinos.B60ProcR = ParseDecimal(TxtDopB60ProcR.Text);
            _selectedDoprinos.B60ProcP = ParseDecimal(TxtDopB60ProcP.Text);
            _selectedDoprinos.Bp60ProcP = ParseDecimal(TxtDopBp60ProcP.Text);
            _selectedDoprinos.Bp60FProcP = ParseDecimal(TxtDopBp60FProcP.Text);
            _selectedDoprinos.PorProcP = ParseDecimal(TxtDopPorProcP.Text);
            _selectedDoprinos.NepProcP = ParseDecimal(TxtDopNepProcP.Text);
            _selectedDoprinos.InvProcP = ParseDecimal(TxtDopInvProcP.Text);
            _selectedDoprinos.Primalac1 = TxtDopPrimalac1.Text.Trim();
            _selectedDoprinos.Primalac2 = TxtDopPrimalac2.Text.Trim();
            _selectedDoprinos.Svrha1 = TxtDopSvrha1.Text.Trim();
            _selectedDoprinos.Svrha2 = TxtDopSvrha2.Text.Trim();
            _selectedDoprinos.ZiroRacun = TxtDopZiroRacun.Text.Trim();
            _selectedDoprinos.PozivNaB = TxtDopPozivNaB.Text.Trim();
            _selectedDoprinos.SifPlac = TxtDopSifPlac.Text.Trim();
            _selectedDoprinos.ZiroRacP = TxtDopZiroRacP.Text.Trim();
            _selectedDoprinos.PozivNa2 = TxtDopPozivNa2.Text.Trim();
            _selectedDoprinos.SifPlacP = TxtDopSifPlacP.Text.Trim();

            // Provjera da li zapis već postoji u bazi
            var existsInDb = _db.Doprinosi.Any(d => d.Id == _selectedDoprinos.Id || 
                (d.Godina == _selectedDoprinos.Godina && d.Mesec == _selectedDoprinos.Mesec && d.RedniBroj == _selectedDoprinos.RedniBroj));

            if (_selectedDoprinos.Id == 0 && !existsInDb)
            {
                _db.Doprinosi.Add(_selectedDoprinos);
            }
            else
            {
                if (_selectedDoprinos.Id == 0)
                {
                    // Učitaj iz baze pošto ključ postoji
                    var dbEntity = _db.Doprinosi.FirstOrDefault(d => 
                        d.Godina == _selectedDoprinos.Godina && 
                        d.Mesec == _selectedDoprinos.Mesec && 
                        d.RedniBroj == _selectedDoprinos.RedniBroj);
                    if (dbEntity != null)
                    {
                        _selectedDoprinos.Id = dbEntity.Id;
                        _db.Entry(dbEntity).CurrentValues.SetValues(_selectedDoprinos);
                    }
                }
                else
                {
                    _db.Entry(_selectedDoprinos).State = Microsoft.EntityFrameworkCore.EntityState.Modified;
                }
            }

            _db.SaveChanges();
            
            // Osveži prikaz u tabeli
            DoprinosiGrid.Items.Refresh();
            
            StatusMessage.Text = $"Doprinos '{_selectedDoprinos.Naziv}' za {_selectedDoprinos.Mesec}.{_selectedDoprinos.Godina} je uspešno sačuvan!";
            MessageBox.Show($"Doprinos '{_selectedDoprinos.Naziv}' za {_selectedDoprinos.Mesec}.{_selectedDoprinos.Godina} je uspešno sačuvan!", "Uspeh", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Greška pri čuvanju doprinosa: {ex.Message}", "Greška", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void UcitajFirmaPodatke()
    {
        try
        {
            _currentFirma = _db.Firme.FirstOrDefault();
            if (_currentFirma == null)
            {
                _currentFirma = new Firma();
            }
            PopuniFirmaFormu();
        }
        catch (Exception ex)
        {
            StatusMessage.Text = $"Greška pri učitavanju podataka o firmi: {ex.Message}";
        }
    }

    private void PopuniFirmaFormu()
    {
        if (_currentFirma == null) return;

        TxtFirmaNaziv.Text = _currentFirma.Naziv;
        TxtFirmaAdresa.Text = _currentFirma.Adresa;
        TxtFirmaGrad.Text = _currentFirma.Grad;
        TxtFirmaPib.Text = _currentFirma.Pib;
        TxtFirmaMb.Text = _currentFirma.Mb;
        TxtFirmaBankovniRacun.Text = _currentFirma.BankovniRacun;
        TxtFirmaSifraPlacanja.Text = _currentFirma.SifraPlacanja;
        TxtFirmaTelefon.Text = _currentFirma.Telefon;
        TxtFirmaEmail.Text = _currentFirma.Email;
        TxtFirmaNapomena.Text = _currentFirma.Napomena;
    }

    private void BtnSacuvajFirma_Click(object sender, RoutedEventArgs e)
    {
        if (_currentFirma == null) return;

        try
        {
            _currentFirma.Naziv = TxtFirmaNaziv.Text.Trim();
            _currentFirma.Adresa = TxtFirmaAdresa.Text.Trim();
            _currentFirma.Grad = TxtFirmaGrad.Text.Trim();
            _currentFirma.Pib = TxtFirmaPib.Text.Trim();
            _currentFirma.Mb = TxtFirmaMb.Text.Trim();
            _currentFirma.BankovniRacun = TxtFirmaBankovniRacun.Text.Trim();
            _currentFirma.SifraPlacanja = TxtFirmaSifraPlacanja.Text.Trim();
            _currentFirma.Telefon = TxtFirmaTelefon.Text.Trim();
            _currentFirma.Email = TxtFirmaEmail.Text.Trim();
            _currentFirma.Napomena = TxtFirmaNapomena.Text.Trim();

            if (_currentFirma.Id == 0)
            {
                _db.Firme.Add(_currentFirma);
            }
            else
            {
                _db.Entry(_currentFirma).State = Microsoft.EntityFrameworkCore.EntityState.Modified;
            }

            _db.SaveChanges();

            // Osveži ime firme u sidebar-u glavnog prozora
            var mainWindow = Application.Current.MainWindow as MainWindow;
            mainWindow?.UcitajImeFirme();

            StatusMessage.Text = "Podaci o firmi su uspešno sačuvani!";
            MessageBox.Show("Podaci o firmi su uspešno sačuvani!", "Uspeh", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Greška pri čuvanju podataka o firmi: {ex.Message}", "Greška", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private decimal ParseDecimal(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        text = text.Replace(".", "").Replace(",", ".").Trim();
        if (decimal.TryParse(text, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal val))
        {
            return val;
        }
        return 0;
    }

    private int ParseInt(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        if (int.TryParse(text.Trim(), out int val))
        {
            return val;
        }
        return 0;
    }

    private void UcitajPlatneRazrede()
    {
        try
        {
            _currentRazredi = _db.PlatniRazredi.FirstOrDefault();
            if (_currentRazredi == null)
            {
                _currentRazredi = new PlatniRazred
                {
                    R1 = 51297.00m, R2 = 51297.00m, R3 = 51297.00m, R4 = 51297.00m, R5 = 51297.00m, R6 = 51297.00m, R7 = 51297.00m, R8 = 51297.00m, R9 = 0m,
                    P1 = 51297.00m, P2 = 51297.00m, P3 = 51297.00m, P4 = 51297.00m, P5 = 51297.00m, P6 = 51297.00m, P7 = 51297.00m, P8 = 51297.00m, P9 = 0m
                };
            }
            PopuniRazrediFormu();
        }
        catch (Exception ex)
        {
            StatusMessage.Text = $"Greška pri učitavanju platnih razreda: {ex.Message}";
        }
    }

    private void PopuniRazrediFormu()
    {
        if (_currentRazredi == null) return;

        TxtR1.Text = _currentRazredi.R1.ToString("N2");
        TxtR2.Text = _currentRazredi.R2.ToString("N2");
        TxtR3.Text = _currentRazredi.R3.ToString("N2");
        TxtR4.Text = _currentRazredi.R4.ToString("N2");
        TxtR5.Text = _currentRazredi.R5.ToString("N2");
        TxtR6.Text = _currentRazredi.R6.ToString("N2");
        TxtR7.Text = _currentRazredi.R7.ToString("N2");
        TxtR8.Text = _currentRazredi.R8.ToString("N2");
        TxtR9.Text = _currentRazredi.R9.ToString("N2");

        TxtP1.Text = _currentRazredi.P1.ToString("N2");
        TxtP2.Text = _currentRazredi.P2.ToString("N2");
        TxtP3.Text = _currentRazredi.P3.ToString("N2");
        TxtP4.Text = _currentRazredi.P4.ToString("N2");
        TxtP5.Text = _currentRazredi.P5.ToString("N2");
        TxtP6.Text = _currentRazredi.P6.ToString("N2");
        TxtP7.Text = _currentRazredi.P7.ToString("N2");
        TxtP8.Text = _currentRazredi.P8.ToString("N2");
        TxtP9.Text = _currentRazredi.P9.ToString("N2");
    }

    private void BtnSacuvajRazredi_Click(object sender, RoutedEventArgs e)
    {
        if (_currentRazredi == null) return;

        try
        {
            _currentRazredi.R1 = ParseDecimal(TxtR1.Text);
            _currentRazredi.R2 = ParseDecimal(TxtR2.Text);
            _currentRazredi.R3 = ParseDecimal(TxtR3.Text);
            _currentRazredi.R4 = ParseDecimal(TxtR4.Text);
            _currentRazredi.R5 = ParseDecimal(TxtR5.Text);
            _currentRazredi.R6 = ParseDecimal(TxtR6.Text);
            _currentRazredi.R7 = ParseDecimal(TxtR7.Text);
            _currentRazredi.R8 = ParseDecimal(TxtR8.Text);
            _currentRazredi.R9 = ParseDecimal(TxtR9.Text);

            _currentRazredi.P1 = ParseDecimal(TxtP1.Text);
            _currentRazredi.P2 = ParseDecimal(TxtP2.Text);
            _currentRazredi.P3 = ParseDecimal(TxtP3.Text);
            _currentRazredi.P4 = ParseDecimal(TxtP4.Text);
            _currentRazredi.P5 = ParseDecimal(TxtP5.Text);
            _currentRazredi.P6 = ParseDecimal(TxtP6.Text);
            _currentRazredi.P7 = ParseDecimal(TxtP7.Text);
            _currentRazredi.P8 = ParseDecimal(TxtP8.Text);
            _currentRazredi.P9 = ParseDecimal(TxtP9.Text);

            if (_currentRazredi.Id == 0)
            {
                _db.PlatniRazredi.Add(_currentRazredi);
            }
            else
            {
                _db.Entry(_currentRazredi).State = Microsoft.EntityFrameworkCore.EntityState.Modified;
            }

            _db.SaveChanges();

            StatusMessage.Text = "Platni razredi su uspešno sačuvani!";
            MessageBox.Show("Platni razredi su uspešno sačuvani!", "Uspeh", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Greška pri čuvanju platnih razreda: {ex.Message}", "Greška", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}

// Marker dummy klasa u slučaju potrebe
internal class DoprunosiWrapSelected { }
