using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;
using ERPiZaradeApp.Services;
using ERPiZaradeData;
using ERPiZaradeData.Models;

namespace ERPiZaradeApp.Views.Ugovori;

/// <summary>
/// Ugovori van radnog odnosa i obračun naknada po njima (Faza 2.3).
///
/// Ekran radi dve stvari koje su namerno razdvojene: vodi <b>ugovore</b>, koji traju, i
/// obračunava <b>naknade</b>, koje pripadaju jednoj isplati. Isti ugovor može biti isplaćen
/// u više rata, i svaka rata ide u svoju isplatu — sa svojom PPP-PD prijavom i svojim BOP-om.
/// </summary>
public partial class UgovoriPage : Page
{
    private PlataDbContext _db;
    private IsplataService _isplateServis;
    private UgovorObracunService _servis;
    private Isplata? _izabranaIsplata;

    /// <summary>
    /// Nema nijednog kartona označenog kao lice van radnog odnosa. Prazna padajuća lista sama
    /// ne kaže zašto je prazna, pa se to izgovara u statusnoj liniji.
    /// </summary>
    private bool _nemaPrimalaca;

    public UgovoriPage()
    {
        InitializeComponent();
        _db = PlataDbContext.Create(AppConfig.DbPath);
        _isplateServis = new IsplataService(_db);
        _servis = new UgovorObracunService(_db);

        ComboTipPrimaoca.ItemsSource = Enum.GetValues<TipPrimaocaPrihoda>()
            .Select(t => new TipPrimaocaStavka { Tip = t })
            .ToList();
        ComboTipPrimaoca.SelectedIndex = 4;   // 05 — nije osiguran po drugom osnovu

        PopuniPeriod();
        Ucitaj();
    }

    private void PopuniPeriod()
    {
        var godine = _db.ObracuniPlata
            .Select(o => o.Godina)
            .Distinct()
            .OrderByDescending(g => g)
            .ToList();

        int tekuca = AppConfig.ActiveGodina ?? DateTime.Now.Year;
        if (!godine.Contains(tekuca)) godine.Insert(0, tekuca);

        ComboGodina.ItemsSource = godine;
        ComboMesec.ItemsSource = Enumerable.Range(1, 12).ToList();

        ComboGodina.SelectedItem = tekuca;
        ComboMesec.SelectedItem = AppConfig.ActiveMesec ?? DateTime.Now.Month;
    }

    private int Godina => ComboGodina.SelectedItem is int g ? g : DateTime.Now.Year;
    private int Mesec => ComboMesec.SelectedItem is int m ? m : DateTime.Now.Month;

    private void ComboPeriod_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        Ucitaj();
    }

    private void ComboIsplata_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        _izabranaIsplata = ComboIsplata.SelectedItem as Isplata;
        UcitajNaknade();
    }

    private void Ucitaj()
    {
        try
        {
            // Samo isplate naknada. Naknada na isplati zarade dobila bi obračunski period
            // meseca ZA KOJI se zarada isplaćuje, a njen period je mesec isplate — zato se
            // isplate zarade ovde uopšte ne nude. Ne poziva se ni Obezbedi: isplata naknada
            // ne nastaje sama, jer joj je datum plaćanja ono što deli prijavu od prijave.
            var isplate = _isplateServis.Isplate(Godina, Mesec, RodIsplate.VanRadnogOdnosa).ToList();
            ComboIsplata.ItemsSource = isplate;
            ComboIsplata.SelectedItem = isplate.FirstOrDefault(i => i.IsplataId == _izabranaIsplata?.IsplataId)
                                        ?? isplate.FirstOrDefault();
            _izabranaIsplata = ComboIsplata.SelectedItem as Isplata;

            ComboVrsta.ItemsSource = _db.VrsteUgovora
                .Where(v => v.Aktivna)
                .OrderBy(v => v.Redosled)
                .ToList();
            if (ComboVrsta.SelectedItem == null) ComboVrsta.SelectedIndex = 0;

            PopuniPrimaoce();
            UcitajUgovore();
            UcitajNaknade();
        }
        catch (Exception ex)
        {
            StatusMessage.Text = $"Greška pri učitavanju: {ex.Message}";
        }
    }

    /// <summary>
    /// Primaoci su <b>sva aktivna lica</b>, a ne samo ona označena poljem „Van radnog odnosa".
    ///
    /// Lice u radnom odnosu sme biti isplaćeno po ugovoru — šifra vrste prihoda za to je
    /// <c>1 01 601 00 0</c>, gde <c>01</c> znači „zaposleni", i <see cref="TipPrimaocaPrihoda"/>
    /// tu vrednost nudi. Dok je ovde stajao filter po oznaci, ta se šifra nije mogla ni
    /// napraviti: zaposleni se nije mogao izabrati.
    ///
    /// Karton je periodičan, pa se uzima poslednji zapis svakog lica — ime se ne menja od
    /// meseca do meseca.
    /// </summary>
    private void PopuniPrimaoce()
    {
        var izabrani = (ComboPrimalac.SelectedItem as PrimalacStavka)?.BrojRadnika;

        // Bivši zaposleni (neaktivan karton) se ne nudi, osim ako je označen kao lice van
        // radnog odnosa — takvi i jesu neaktivni u smislu zarade, a primaoci jesu.
        var primaoci = _db.Radnici
            .Where(r => r.VanRadnogOdnosa || r.Aktivan)
            .OrderByDescending(r => r.Godina).ThenByDescending(r => r.Mesec)
            .ToList()
            .GroupBy(r => r.BrojRadnika)
            .Select(g => new PrimalacStavka
            {
                BrojRadnika = g.Key,
                ImeIPrezime = g.First().ImeIPrezime,
                URadnomOdnosu = !g.First().VanRadnogOdnosa
            })
            .OrderBy(p => p.BrojRadnika)
            .ToList();

        ComboPrimalac.ItemsSource = primaoci;
        ComboPrimalac.SelectedItem = primaoci.FirstOrDefault(p => p.BrojRadnika == izabrani)
                                     ?? primaoci.FirstOrDefault();

        _nemaPrimalaca = primaoci.Count == 0;
    }

    private void UcitajUgovore()
    {
        var ugovori = _db.Ugovori
            .Include(u => u.VrstaUgovora)
            .OrderByDescending(u => u.DatumZakljucenja)
            .ThenBy(u => u.BrojRadnika)
            .ToList();

        var imena = _db.Radnici
            .OrderByDescending(r => r.Godina).ThenByDescending(r => r.Mesec)
            .ToList()
            .GroupBy(r => r.BrojRadnika)
            .ToDictionary(g => g.Key, g => g.First().ImeIPrezime);

        var isplaceno = _servis.IsplacenoPoUgovorima();

        var redovi = ugovori.Select(u => new UgovorRed
        {
            Ugovor = u,
            Primalac = imena.TryGetValue(u.BrojRadnika, out string? ime)
                ? ime
                : $"(nema kartona za #{u.BrojRadnika})",
            Svp = SvpService.Sastavi(u.TipPrimaoca, u.VrstaUgovora?.Ovp) is { Length: 9 } svp ? svp : "—",
            BrojIsplata = isplaceno.TryGetValue(u.UgovorId, out var x) ? x.BrojIsplata : 0,
            IsplaceniBruto = isplaceno.TryGetValue(u.UgovorId, out var y) ? y.Bruto : 0m
        }).ToList();

        GridUgovori.ItemsSource = redovi;

        int bezSvp = redovi.Count(r => r.Ugovor.Aktivan && r.Svp == "—");

        if (_nemaPrimalaca)
        {
            StatusMessage.Text =
                "Nema nijednog primaoca. Ugovor se zaključuje sa licem koje u meniju „Radnici\" ima " +
                "karton označen poljem „Van radnog odnosa\" — odatle se uzimaju JMBG, opština " +
                "prebivališta i tekući račun, bez kojih nema ni prijave ni isplate.";
            return;
        }

        StatusMessage.Text = bezSvp == 0
            ? $"{redovi.Count} ugovora."
            : $"{redovi.Count} ugovora; {bezSvp} bez šifre vrste prihoda — dopunite OVP u šifarniku vrsta ugovora.";
    }

    /// <summary>
    /// Nova isplata naknada. Traži se <b>datum isplate</b>, jer je on datum plaćanja na PPP-PD
    /// prijavi (polje 1.4) i jedino po čemu se jedna prijava razlikuje od druge; mesec iz njega
    /// je obračunski period te prijave (polje 1.2).
    /// </summary>
    private void BtnNovaIsplata_Click(object sender, RoutedEventArgs e)
    {
        var prozor = new NovaIsplataNaknadeWindow(Godina, Mesec) { Owner = Window.GetWindow(this) };
        if (prozor.ShowDialog() != true) return;

        var rezultat = _isplateServis.DodajNaknadu(
            prozor.DatumIsplate.Year, prozor.DatumIsplate.Month, prozor.Opis, prozor.DatumIsplate);

        StatusMessage.Text = rezultat.Poruka;

        if (!rezultat.Uspesno)
        {
            MessageBox.Show(rezultat.Poruka, "Isplata nije dodata", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Datum može da odvede u drugi mesec od izabranog — period se tada pomera za njim,
        // jer je mesec isplate obračunski period prijave, a ne stvar izbora na ekranu.
        if (prozor.DatumIsplate.Year != Godina || prozor.DatumIsplate.Month != Mesec)
        {
            if (!ComboGodina.Items.Contains(prozor.DatumIsplate.Year))
                ComboGodina.Items.Add(prozor.DatumIsplate.Year);

            ComboGodina.SelectedItem = prozor.DatumIsplate.Year;
            ComboMesec.SelectedItem = prozor.DatumIsplate.Month;
        }

        _izabranaIsplata = rezultat.Isplata;
        Ucitaj();
    }

    /// <summary>
    /// Briše izabranu isplatu naknada. Sva ograničenja stoje u <c>IsplataService.Obrisi</c> —
    /// briše se samo poslednja u mesecu, i samo dok nema ni obračuna ni prijave — pa se ovde
    /// ne ponavljaju: prepisano pravilo je ono koje se prvo razilazi.
    /// </summary>
    private void BtnObrisiIsplatu_Click(object sender, RoutedEventArgs e)
    {
        if (_izabranaIsplata == null)
        {
            StatusMessage.Text = "Nema izabrane isplate naknada.";
            return;
        }

        if (MessageBox.Show(
                $"Obrisati isplatu „{_izabranaIsplata.Naziv}“ od {_izabranaIsplata.DatumIsplate:dd.MM.yyyy}?",
                "Potvrda", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        var rezultat = _isplateServis.Obrisi(_izabranaIsplata.IsplataId);
        StatusMessage.Text = rezultat.Poruka;

        if (!rezultat.Uspesno)
        {
            MessageBox.Show(rezultat.Poruka, "Isplata nije obrisana",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _izabranaIsplata = null;
        Ucitaj();
    }

    private void UcitajNaknade()
    {
        if (_izabranaIsplata == null)
        {
            GridNaknade.ItemsSource = null;
            NaslovNaknada.Text = "OBRAČUNATE NAKNADE — nema isplate naknada u ovom mesecu (➕ da je dodate)";
            return;
        }

        var naknade = IsplataService
            .Obuhvat(
                _db.ObracuniPlata
                    .Include(o => o.Radnik)
                    .Include(o => o.Ugovor!).ThenInclude(u => u.VrstaUgovora),
                Godina, Mesec, _izabranaIsplata)
            .Where(o => o.UgovorId != null)
            .ToList()
            .Select(o => new NaknadaRed
            {
                ObracunId = o.Id,
                Primalac = o.Radnik?.ImeIPrezime ?? "",
                Vrsta = o.Ugovor?.VrstaUgovora?.Naziv ?? "",
                Svp = SvpService.Odredi(o) is { Length: 9 } svp ? svp : "—",
                Bruto = o.BrutoZarada,
                Osnovica = o.OsnovicaDoprinosa ?? 0m,
                Porez = o.PorezNaDohodak,
                Doprinosi = o.UkupniDoprinosi + o.UkupniDoprinosiPoslodavca,
                Neto = o.NetoIsplata,
                Zakljucan = o.Zakljucan,
                Storniran = o.Storniran
            })
            .ToList();

        GridNaknade.ItemsSource = naknade;

        NaslovNaknada.Text = $"OBRAČUNATE NAKNADE — {_izabranaIsplata.Naziv} " +
                             $"({naknade.Count(n => !n.Storniran)}, bruto {naknade.Where(n => !n.Storniran).Sum(n => n.Bruto):N2})";
    }

    // ── Ugovori ──────────────────────────────────────────────────────

    private void BtnDodaj_Click(object sender, RoutedEventArgs e)
    {
        if (ComboVrsta.SelectedItem is not VrstaUgovora vrsta)
        {
            StatusMessage.Text = "Izaberite vrstu ugovora.";
            return;
        }

        if (ComboPrimalac.SelectedItem is not PrimalacStavka primalac)
        {
            MessageBox.Show(
                "Nema nijednog lica označenog kao primalac po ugovoru.\n\n" +
                "Unesite ga u „Radnici\" i označite poljem „Van radnog odnosa\" — otuda se uzimaju " +
                "JMBG, opština prebivališta i tekući račun bez kojih nema ni prijave ni isplate.",
                "Nema primalaca", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!ProcitajIznos(TxtIznos.Text, out decimal iznos) || iznos <= 0)
        {
            StatusMessage.Text = "Unesite ugovoreni iznos veći od nule.";
            return;
        }

        var tip = (ComboTipPrimaoca.SelectedItem as TipPrimaocaStavka)?.Tip
                  ?? TipPrimaocaPrihoda.NijeOsiguranPoDrugomOsnovu;

        var ugovor = new Ugovor
        {
            VrstaUgovoraId = vrsta.VrstaUgovoraId,
            BrojRadnika = primalac.BrojRadnika,
            TipPrimaoca = tip,
            Predmet = TxtPredmet.Text.Trim(),
            UgovorenIznos = iznos,
            IznosJeNeto = ChkNeto.IsChecked == true,
            DatumZakljucenja = DateTime.Today,
            DatumOd = new DateTime(Godina, Mesec, 1),
            Aktivan = true
        };

        try
        {
            _db.Ugovori.Add(ugovor);
            _db.SaveChanges();

            TxtPredmet.Text = "";
            TxtIznos.Text = "";

            UcitajUgovore();
            StatusMessage.Text = $"Dodat ugovor za {primalac.ImeIPrezime} — {vrsta.Naziv}.";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ugovor nije dodat: {ex.Message}", "Greška",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Unos primaoca sa ovog ekrana. Isto se može uraditi i u „Radnici", ali tek pošto se
    /// karton otvori za izmenu — previše koraka za jedan čekboks, i previše prilika da se
    /// zaboravi snimanje.
    /// </summary>
    private void BtnNoviPrimalac_Click(object sender, RoutedEventArgs e)
    {
        var prozor = new PrimalacWindow(Godina, Mesec) { Owner = Window.GetWindow(this) };

        if (prozor.ShowDialog() != true) return;

        // Karton je upisan u zasebnom kontekstu, pa se ovaj osvežava da ga vidi.
        _db = PlataDbContext.Create(AppConfig.DbPath);
        _isplateServis = new IsplataService(_db);
        _servis = new UgovorObracunService(_db);

        PopuniPrimaoce();
        UcitajUgovore();

        if (ComboPrimalac.ItemsSource is IEnumerable<PrimalacStavka> stavke)
        {
            ComboPrimalac.SelectedItem = stavke
                .FirstOrDefault(p => p.BrojRadnika == prozor.BrojRadnika) ?? ComboPrimalac.SelectedItem;
        }

        StatusMessage.Text = $"Primalac je unet i izabran. Popunite predmet i iznos, pa ➕ dodajte ugovor.";
    }

    private void BtnObrisi_Click(object sender, RoutedEventArgs e)
    {
        if (GridUgovori.SelectedItem is not UgovorRed red)
        {
            StatusMessage.Text = "Izaberite ugovor koji želite da obrišete.";
            return;
        }

        // Obračun po ugovoru je dokaz šta je isplaćeno i prijavljeno; bez ugovora bi ostao
        // bez šifre vrste prihoda i bez stopa po kojima je nastao.
        if (red.BrojIsplata > 0)
        {
            MessageBox.Show(
                $"Po ovom ugovoru je obračunato {red.BrojIsplata} naknada i ne može se obrisati.\n\n" +
                "Isključite ga poljem „Aktivan\" da se više ne nudi za obračun.",
                "Ugovor je u upotrebi", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var ugovor = _db.Ugovori.FirstOrDefault(u => u.UgovorId == red.UgovorId);
            if (ugovor == null) return;

            _db.Ugovori.Remove(ugovor);
            _db.SaveChanges();

            UcitajUgovore();
            StatusMessage.Text = "Ugovor je obrisan.";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ugovor nije obrisan: {ex.Message}", "Greška",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Tekst ugovora. Otvara se i za ugovor koji ga još nema — tamo se i generiše, jer je to
    /// jedino mesto gde se vidi šta je šablon popunio a šta nije.
    /// </summary>
    private void BtnDokument_Click(object sender, RoutedEventArgs e)
    {
        if (GridUgovori.SelectedItem is not UgovorRed red)
        {
            StatusMessage.Text = "Izaberite ugovor čiji tekst želite da otvorite.";
            return;
        }

        var prozor = new UgovorDokumentWindow(red.UgovorId) { Owner = Window.GetWindow(this) };
        prozor.ShowDialog();

        // Tekst je mogao biti sačuvan u zasebnom kontekstu, pa se tabela osvežava.
        UcitajUgovore();
    }

    private void BtnSacuvaj_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _db.SaveChanges();

            _db = PlataDbContext.Create(AppConfig.DbPath);
            _isplateServis = new IsplataService(_db);
            _servis = new UgovorObracunService(_db);
            Ucitaj();

            StatusMessage.Text = "Ugovori su sačuvani.";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ugovori nisu sačuvani: {ex.Message}", "Greška",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ── Obračun naknade ──────────────────────────────────────────────

    private void GridUgovori_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GridUgovori.SelectedItem is UgovorRed red)
        {
            TxtIznosObracuna.Text = red.UgovorenIznos.ToString("0.00", CultureInfo.CurrentCulture);
            ChkNetoObracun.IsChecked = red.Ugovor.IznosJeNeto;
        }

        PrikaziRacunicu();
    }

    private void IznosObracuna_TextChanged(object sender, TextChangedEventArgs e) => PrikaziRacunicu();

    private void ChkNetoObracun_Click(object sender, RoutedEventArgs e) => PrikaziRacunicu();

    /// <summary>
    /// Računica pre upisa. Isti razlog kao proba pri prevođenju obračuna na stavke: radi se
    /// o novcu koji ide fizičkom licu i prijavljuje se Poreskoj upravi, pa se vidi pre nego
    /// što se upiše.
    /// </summary>
    private void PrikaziRacunicu()
    {
        if (!IsLoaded) return;

        if (GridUgovori.SelectedItem is not UgovorRed red || red.Ugovor.VrstaUgovora == null)
        {
            TxtRacunica.Text = "Izaberite ugovor da bi se videla računica.";
            return;
        }

        if (!ProcitajIznos(TxtIznosObracuna.Text, out decimal iznos) || iznos <= 0)
        {
            TxtRacunica.Text = "Unesite iznos naknade.";
            return;
        }

        var vrsta = red.Ugovor.VrstaUgovora;

        try
        {
            var racunica = ChkNetoObracun.IsChecked == true
                ? UgovorObracunService.Izracunaj(vrsta, UgovorObracunService.BrutoIzNeta(vrsta, iznos))
                : UgovorObracunService.Izracunaj(vrsta, iznos);

            var sb = new StringBuilder();
            sb.AppendLine($"{vrsta.Naziv}");
            sb.AppendLine($"SVP  {red.Svp}");
            sb.AppendLine();
            sb.AppendLine(Red("Bruto naknada", racunica.Bruto));
            sb.AppendLine(Red($"Normirani troškovi {vrsta.NormiraniTroskoviProcenat:N0}%", -racunica.NormiraniTroskovi));
            sb.AppendLine(Red("Osnovica", racunica.Osnovica));
            sb.AppendLine();
            sb.AppendLine(Red($"Porez {vrsta.StopaPoreza:N2}%", -racunica.Porez));

            if (racunica.PioPrimalac != 0) sb.AppendLine(Red($"PIO {vrsta.StopaPioPrimalac:N2}%", -racunica.PioPrimalac));
            if (racunica.ZdravstvoPrimalac != 0) sb.AppendLine(Red($"Zdravstveno {vrsta.StopaZdravstvoPrimalac:N2}%", -racunica.ZdravstvoPrimalac));
            if (racunica.NezaposlenostPrimalac != 0) sb.AppendLine(Red($"Nezaposlenost {vrsta.StopaNezaposlenostPrimalac:N2}%", -racunica.NezaposlenostPrimalac));

            sb.AppendLine();
            sb.AppendLine(Red("NETO ZA ISPLATU", racunica.Neto));

            if (racunica.DoprinosiIsplatioca != 0)
            {
                sb.AppendLine();
                sb.AppendLine("Na teret isplatioca:");
                if (racunica.PioIsplatilac != 0) sb.AppendLine(Red($"PIO {vrsta.StopaPioIsplatilac:N2}%", racunica.PioIsplatilac));
                if (racunica.ZdravstvoIsplatilac != 0) sb.AppendLine(Red($"Zdravstveno {vrsta.StopaZdravstvoIsplatilac:N2}%", racunica.ZdravstvoIsplatilac));
                if (racunica.NezaposlenostIsplatilac != 0) sb.AppendLine(Red($"Nezaposlenost {vrsta.StopaNezaposlenostIsplatilac:N2}%", racunica.NezaposlenostIsplatilac));
            }

            sb.AppendLine();
            sb.AppendLine(Red("Ukupan trošak", racunica.UkupanTrosak));
            sb.AppendLine(Red("Porezi i doprinosi", racunica.PoreziIDoprinosi));

            TxtRacunica.Text = sb.ToString();
        }
        catch (InvalidOperationException ex)
        {
            TxtRacunica.Text = ex.Message;
        }
    }

    private static string Red(string naziv, decimal iznos) => $"{naziv,-28}{iznos,14:N2}";

    private void BtnObracunaj_Click(object sender, RoutedEventArgs e)
    {
        if (GridUgovori.SelectedItem is not UgovorRed red)
        {
            StatusMessage.Text = "Izaberite ugovor koji se obračunava.";
            return;
        }

        if (_izabranaIsplata == null)
        {
            StatusMessage.Text = "Izaberite isplatu kojoj naknada pripada.";
            return;
        }

        if (!ProcitajIznos(TxtIznosObracuna.Text, out decimal iznos) || iznos <= 0)
        {
            StatusMessage.Text = "Unesite iznos naknade veći od nule.";
            return;
        }

        if (!red.Ugovor.Aktivan)
        {
            MessageBox.Show("Ugovor je isključen i ne obračunava se. Uključite ga poljem „Aktivan\".",
                "Ugovor nije aktivan", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var rezultat = _servis.Obracunaj(red.UgovorId, _izabranaIsplata.IsplataId, iznos,
            ChkNetoObracun.IsChecked == true);

        StatusMessage.Text = rezultat.Poruka;

        if (!rezultat.Uspesno)
        {
            MessageBox.Show(rezultat.Poruka, "Naknada nije obračunata",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        UcitajUgovore();
        UcitajNaknade();
    }

    private void BtnObrisiNaknadu_Click(object sender, RoutedEventArgs e)
    {
        if (GridNaknade.SelectedItem is not NaknadaRed red)
        {
            StatusMessage.Text = "Izaberite obračunatu naknadu koju želite da obrišete.";
            return;
        }

        // Zaključan obračun se ne briše — nad njim je dozvoljeno samo storniranje, isto
        // pravilo koje važi i za zaradu.
        if (red.Zakljucan)
        {
            MessageBox.Show(
                "Naknada je u zaključanom obračunu. Zaključan obračun se ne briše — stornirajte ga " +
                "na ekranu „Obračun plate\", pa ostaje trag da je bio obračunat.",
                "Obračun je zaključan", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (MessageBox.Show(
                $"Obrisati obračunatu naknadu za {red.Primalac} (bruto {red.Bruto:N2})?",
                "Potvrda brisanja", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            var obracun = _db.ObracuniPlata.FirstOrDefault(o => o.Id == red.ObracunId);
            if (obracun == null) return;

            _db.ObracuniPlata.Remove(obracun);
            _db.SaveChanges();

            UcitajUgovore();
            UcitajNaknade();
            StatusMessage.Text = "Obračunata naknada je obrisana.";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Naknada nije obrisana: {ex.Message}", "Greška",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>Čita iznos i sa zarezom i sa tačkom — unosi se i jedno i drugo.</summary>
    private static bool ProcitajIznos(string? tekst, out decimal iznos)
    {
        iznos = 0m;
        if (string.IsNullOrWhiteSpace(tekst)) return false;

        string ocisceno = tekst.Trim().Replace(" ", "").Replace(".", ",");
        return decimal.TryParse(ocisceno, NumberStyles.Number, new CultureInfo("sr-Latn-RS"), out iznos)
               || decimal.TryParse(tekst.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out iznos);
    }
}
