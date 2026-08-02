using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;
using PlataData;
using PlataData.Models;

namespace PlataApp.Views.Doprinosi;

public partial class DoprinosiPage : Page
{
    private PlataDbContext _db;
    private List<Doprinos> _currentDoprinosi = new();
    private Doprinos? _selectedDoprinos;

    public DoprinosiPage()
    {
        InitializeComponent();
        _db = PlataDbContext.Create(AppConfig.DbPath);

        InicijalizujPeriodSelectore();
        UcitajTrenutniPeriod();
    }

    private void InicijalizujPeriodSelectore()
    {
        var years = Enumerable.Range(2000, 31).ToList();
        try
        {
            var dbYears = _db.Doprinosi.Select(d => d.Godina).Distinct().ToList();
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
            int defGodina;
            int defMesec;

            if (AppConfig.ActiveGodina.HasValue && AppConfig.ActiveMesec.HasValue)
            {
                defGodina = AppConfig.ActiveGodina.Value;
                defMesec = AppConfig.ActiveMesec.Value;
            }
            else
            {
                var latestObracun = _db.ObracuniPlata
                    .OrderByDescending(o => o.Godina)
                    .ThenByDescending(o => o.Mesec)
                    .FirstOrDefault();

                defGodina = latestObracun?.Godina ?? DateTime.Now.Year;
                defMesec = latestObracun?.Mesec ?? DateTime.Now.Month;
            }

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
            _currentDoprinosi = _db.Doprinosi
                .Where(d => d.Godina == godina && d.Mesec == mesec)
                .OrderBy(d => d.RedniBroj)
                .ToList();

            bool doprinosiNisuPostojali = false;
            if (!_currentDoprinosi.Any())
            {
                doprinosiNisuPostojali = true;
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

            DoprinosiGrid.ItemsSource = null;
            DoprinosiGrid.ItemsSource = _currentDoprinosi;

            if (_currentDoprinosi.Any())
            {
                DoprinosiGrid.SelectedItem = _currentDoprinosi.First();
            }
            else
            {
                ClearDoprinosiForm();
            }

            if (doprinosiNisuPostojali)
            {
                StatusMessage.Text = $"Doprinosi za {mesec}.{godina} nisu postojali u bazi. Kopirani su iz prethodnog perioda ili kreirani na osnovu šablona.";
            }
            else
            {
                StatusMessage.Text = $"Uspešno učitani doprinosi za {mesec}.{godina}.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage.Text = $"Greška pri učitavanju doprinosa: {ex.Message}";
        }
    }

    private void BtnUcitaj_Click(object sender, RoutedEventArgs e)
    {
        if (ComboGodina.SelectedItem is int godina && ComboMesec.SelectedItem is int mesec)
        {
            UcitajParametre(godina, mesec);
            AppConfig.ActiveGodina = godina;
            AppConfig.ActiveMesec = mesec;
        }
    }

    private void DoprinosiGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DoprinosiGrid.SelectedItem is Doprinos d)
        {
            PopuniDoprinosiFormu(d);
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

        bool isLocked = _db.ObracuniPlata.Any(o => o.Godina == _selectedDoprinos.Godina && o.Mesec == _selectedDoprinos.Mesec && o.Zakljucan);
        if (isLocked)
        {
            MessageBox.Show("Obračunski period je ZAKLJUČAN. Izmene parametara nisu dozvoljene.", "Upozorenje", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

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
            
            DoprinosiGrid.Items.Refresh();
            
            StatusMessage.Text = $"Doprinos '{_selectedDoprinos.Naziv}' za {_selectedDoprinos.Mesec}.{_selectedDoprinos.Godina} je uspešno sačuvan!";
            MessageBox.Show($"Doprinos '{_selectedDoprinos.Naziv}' za {_selectedDoprinos.Mesec}.{_selectedDoprinos.Godina} je uspešno sačuvan!", "Uspeh", MessageBoxButton.OK, MessageBoxImage.Information);

            // AUTOMATSKO PRERAČUNAVANJE AKO POSTOJE OBRAČUNI
            int godina = _selectedDoprinos.Godina;
            int mesec = _selectedDoprinos.Mesec;

            var radniSatiList = _db.RadniSati
                .Include(s => s.Radnik)
                .Where(s => s.Godina == godina && s.Mesec == mesec)
                .ToList();

            if (radniSatiList.Count > 0)
            {
                var rez = MessageBox.Show(
                    $"Izmenili ste stope doprinosa za period {mesec}.{godina}.\n\n" +
                    $"Pronađeno je {radniSatiList.Count} obračunatih plata za ovaj period.\n" +
                    $"Da li želite da automatski preračunate sve plate sa novim stopama doprinosa?",
                    "Preračunavanje plata",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question
                );

                if (rez == MessageBoxResult.Yes)
                {
                    try
                    {
                        var obracunService = new PlataApp.Services.ObracunService(_db);

                        var porezi = _db.Porezi.FirstOrDefault(p => p.Godina == godina && p.Mesec == mesec);
                        if (porezi == null)
                        {
                            porezi = _db.Porezi
                                .Where(p => p.Godina < godina || (p.Godina == godina && p.Mesec < mesec))
                                .OrderByDescending(p => p.Godina)
                                .ThenByDescending(p => p.Mesec)
                                .FirstOrDefault();
                        }

                        decimal vrednostBoda = porezi?.VrBoda ?? 1860.34m;
                        int fondSati = porezi?.FondCasova ?? 176;
                        int updatedCount = 0;

                        foreach (var rs in radniSatiList)
                        {
                            var radnik = rs.Radnik;
                            if (radnik == null) continue;

                            var postojeciObracun = _db.ObracuniPlata
                                .FirstOrDefault(o => o.RadnikId == rs.RadnikId && o.Godina == godina && o.Mesec == mesec);

                            var noviObracun = obracunService.Calculate(radnik, rs, godina, mesec, vrednostBoda, fondSati);

                            if (postojeciObracun != null)
                            {
                                postojeciObracun.BrutoZarada = noviObracun.BrutoZarada;
                                postojeciObracun.BrutoBolovanje = noviObracun.BrutoBolovanje;
                                postojeciObracun.BrutoNaknade = noviObracun.BrutoNaknade;
                                postojeciObracun.BrutoStimulacija = noviObracun.BrutoStimulacija;
                                postojeciObracun.BrutoMinuliRad = noviObracun.BrutoMinuliRad;

                                postojeciObracun.NetoZar = noviObracun.NetoZar;
                                postojeciObracun.NetoNerd = noviObracun.NetoNerd;
                                postojeciObracun.NetoGOd = noviObracun.NetoGOd;
                                postojeciObracun.NetoTo = noviObracun.NetoTo;
                                postojeciObracun.TopliObrokIznos = noviObracun.TopliObrokIznos;
                                postojeciObracun.NetoReg = noviObracun.NetoReg;
                                postojeciObracun.Neto = noviObracun.Neto;
                                postojeciObracun.NetoBol = noviObracun.NetoBol;
                                postojeciObracun.NetoB100 = noviObracun.NetoB100;
                                postojeciObracun.NetoPlac = noviObracun.NetoPlac;
                                postojeciObracun.NetoPlZ = noviObracun.NetoPlZ;
                                postojeciObracun.NetoDrza = noviObracun.NetoDrza;
                                postojeciObracun.NetoNocni = noviObracun.NetoNocni;
                                postojeciObracun.NetoVezba = noviObracun.NetoVezba;
                                postojeciObracun.NetoPrek = noviObracun.NetoPrek;
                                postojeciObracun.NetoTer = noviObracun.NetoTer;
                                postojeciObracun.KorDod = noviObracun.KorDod;
                                postojeciObracun.KorDod1 = noviObracun.KorDod1;
                                postojeciObracun.Kumul = noviObracun.Kumul;
                                postojeciObracun.NetoNede = noviObracun.NetoNede;

                                postojeciObracun.DoprinosPioRadnik = noviObracun.DoprinosPioRadnik;
                                postojeciObracun.DoprinosZdravstvoRadnik = noviObracun.DoprinosZdravstvoRadnik;
                                postojeciObracun.DoprinosNezaposlenostRadnik = noviObracun.DoprinosNezaposlenostRadnik;

                                postojeciObracun.DoprinosPioPoslodavac = noviObracun.DoprinosPioPoslodavac;
                                postojeciObracun.DoprinosZdravstvoPoslodavac = noviObracun.DoprinosZdravstvoPoslodavac;
                                postojeciObracun.DoprinosNezaposlenostPoslodavac = noviObracun.DoprinosNezaposlenostPoslodavac;

                                postojeciObracun.PorezNaDohodak = noviObracun.PorezNaDohodak;
                                postojeciObracun.PoreskaOsnovica = noviObracun.PoreskaOsnovica;
                                postojeciObracun.LicniOdbitak = noviObracun.LicniOdbitak;
                                postojeciObracun.KreditObustava = noviObracun.KreditObustava;
                                postojeciObracun.Samodoprinosi = noviObracun.Samodoprinosi;
                                postojeciObracun.OstaliOdbici = noviObracun.OstaliOdbici;
                                postojeciObracun.NetoIsplata = noviObracun.NetoIsplata;

                                postojeciObracun.RedovniSati = rs.RedovniSati;
                                postojeciObracun.BolovanjeSati = rs.BolovanjeSati;
                                postojeciObracun.PrekovremeneSati = rs.PrekovremeneSati;
                                postojeciObracun.GodisnjioOdmorSati = rs.GodisnjiOdmorSati;
                                postojeciObracun.DrzavniPraznikSati = rs.DrzavniPraznikSati;
                                postojeciObracun.NocniSati = rs.NocniSati;
                                postojeciObracun.SmenskiSati = rs.SmenskiSati;
                                postojeciObracun.RadPraznikomSati = rs.RadPraznikomSati;
                                postojeciObracun.NocniRadPraznikomSati = rs.NocniRadPraznikomSati;
                                postojeciObracun.PlacenoOdsustvoSati = rs.PlacenoOdsustvoSati;
                                postojeciObracun.NedeljaSati = rs.RadNedeljomSati;
                                postojeciObracun.PlacenoZakonskiSatiLegacy = rs.PlacenoZakonskiSati;
                                postojeciObracun.BolovanjePreko60SatiLegacy = rs.BolovanjePreko60Sati;
                                postojeciObracun.PorodiljskoOdsustvoSatiLegacy = rs.PorodiljskoOdsustvoSati;
                                postojeciObracun.Bolovanje100SatiLegacy = rs.Bolovanje100Sati;
                                postojeciObracun.Varijabila = rs.Varijabila;

                                postojeciObracun.Prosek = noviObracun.Prosek;
                                postojeciObracun.DatumObracuna = DateTime.Now;
                                postojeciObracun.Napomena = $"Ažurirano izmenom doprinosa {DateTime.Now:dd.MM.yyyy HH:mm}";

                                _db.Entry(postojeciObracun).State = Microsoft.EntityFrameworkCore.EntityState.Modified;
                            }
                            else
                            {
                                _db.ObracuniPlata.Add(noviObracun);
                            }

                            updatedCount++;
                        }

                        _db.SaveChanges();
                        MessageBox.Show(
                            $"Uspešno preračunate plate za {updatedCount} zaposlenih sa novim stopama doprinosa.",
                            "Uspeh",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information
                        );
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Greška prilikom preračunavanja plata: {ex.Message}", "Greška", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Greška pri čuvanju doprinosa: {ex.Message}", "Greška", MessageBoxButton.OK, MessageBoxImage.Error);
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
}
