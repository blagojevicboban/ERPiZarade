using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;
using PlataData;
using PlataData.Models;

namespace PlataApp.Views.Porezi;

public partial class PoreziPage : Page
{
    private PlataDbContext _db;
    private PlataData.Models.Porezi? _currentParams;

    public PoreziPage()
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
            _currentParams = _db.Porezi
                .FirstOrDefault(p => p.Godina == godina && p.Mesec == mesec);

            bool poreskiNisuPostojali = false;
            if (_currentParams == null)
            {
                poreskiNisuPostojali = true;
                var fallback = _db.Porezi
                    .Where(p => p.Godina < godina || (p.Godina == godina && p.Mesec < mesec))
                    .OrderByDescending(p => p.Godina)
                    .ThenByDescending(p => p.Mesec)
                    .FirstOrDefault();

                if (fallback != null)
                {
                    _currentParams = new PlataData.Models.Porezi
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
                    _currentParams = new PlataData.Models.Porezi
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

            if (poreskiNisuPostojali)
            {
                StatusMessage.Text = $"Parametri za {mesec}.{godina} nisu postojali u bazi. Kopirani su iz prethodnog perioda ili kreirani na osnovu šablona.";
            }
            else
            {
                StatusMessage.Text = $"Uspešno učitani poreski parametri za {mesec}.{godina}.";
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
            AppConfig.ActiveGodina = godina;
            AppConfig.ActiveMesec = mesec;
        }
    }

    private void BtnSacuvaj_Click(object sender, RoutedEventArgs e)
    {
        if (_currentParams == null) return;

        bool isLocked = _db.ObracuniPlata.Any(o => o.Godina == _currentParams.Godina && o.Mesec == _currentParams.Mesec && o.Zakljucan);
        if (isLocked)
        {
            MessageBox.Show("Obračunski period je ZAKLJUČAN. Izmene parametara nisu dozvoljene.", "Upozorenje", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

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

            // AUTOMATSKO PRERAČUNAVANJE AKO POSTOJE OBRAČUNI
            int godina = _currentParams.Godina;
            int mesec = _currentParams.Mesec;

            var radniSatiList = _db.RadniSati
                .Include(s => s.Radnik)
                .Where(s => s.Godina == godina && s.Mesec == mesec)
                .ToList();

            if (radniSatiList.Count > 0)
            {
                var rez = MessageBox.Show(
                    $"Izmenili ste opšte poreske parametre za period {mesec}.{godina}.\n\n" +
                    $"Pronađeno je {radniSatiList.Count} obračunatih plata za ovaj period.\n" +
                    $"Da li želite da automatski preračunate sve plate sa novim poreskim parametrima?",
                    "Preračunavanje plata",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question
                );

                if (rez == MessageBoxResult.Yes)
                {
                    try
                    {
                        var obracunService = new PlataApp.Services.ObracunService(_db);
                        decimal vrednostBoda = _currentParams.VrBoda;
                        int fondSati = _currentParams.FondCasova;
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
                                postojeciObracun.Napomena = $"Ažurirano izmenom poreskih parametara {DateTime.Now:dd.MM.yyyy HH:mm}";

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
                            $"Uspešno preračunate plate za {updatedCount} zaposlenih sa novim poreskim parametrima.",
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
            MessageBox.Show($"Greška pri čuvanju poreskih podešavanja: {ex.Message}", "Greška", MessageBoxButton.OK, MessageBoxImage.Error);
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
}
