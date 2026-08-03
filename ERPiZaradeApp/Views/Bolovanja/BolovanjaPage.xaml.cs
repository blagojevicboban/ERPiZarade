using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using ERPiZaradeApp.Services;
using ERPiZaradeData;
using ERPiZaradeData.Models;

namespace ERPiZaradeApp.Views.Bolovanja;

/// <summary>Radnik u padajućoj listi; karton je periodičan, pa se pamti broj radnika.</summary>
public sealed class RadnikStavka
{
    public required int BrojRadnika { get; init; }
    public required string ImeIPrezime { get; init; }
    public string Naziv => $"{BrojRadnika}. {ImeIPrezime}";
}

/// <summary>Osnov sprečenosti u padajućoj listi.</summary>
public sealed class OsnovStavka
{
    public required OsnovSprecenosti Osnov { get; init; }
    public string Naziv => Bolovanje.NazivOsnova(Osnov);
}

/// <summary>
/// Bolovanja preko 30 dana i obrasci za refundaciju iz sredstava RFZO (Faza 2.6).
///
/// Ekran vodi <b>evidenciju</b> — za koje dane i po kom osnovu je naknada isplaćena — a
/// iznose čita iz obračuna. Zbog toga se ovde ne unosi nijedan dinar: kad bi se unosio,
/// poslodavac bi Fondu prijavio jedno, a Poreskoj upravi kroz PPP-PD drugo.
/// </summary>
public partial class BolovanjaPage : Page
{
    private readonly PlataDbContext _db;
    private readonly RfzoService _servis;
    private Oz10Spisak? _spisak;

    public BolovanjaPage()
    {
        InitializeComponent();
        _db = PlataDbContext.Create(AppConfig.DbPath);
        _servis = new RfzoService(_db);

        ComboOsnov.ItemsSource = Enum.GetValues<OsnovSprecenosti>()
            .Select(o => new OsnovStavka { Osnov = o })
            .ToList();
        ComboOsnov.SelectedIndex = 0;

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

    private void Ucitaj()
    {
        try
        {
            PopuniRadnike();
            PredlozeniDatumi();

            _spisak = _servis.Pripremi(Godina, Mesec);

            GridRedovi.ItemsSource = _spisak.Redovi;
            GridNalazi.ItemsSource = _spisak.Nalazi;

            StatusMessage.Text = _spisak.Redovi.Count == 0
                ? $"Za {Mesec:D2}/{Godina} nije evidentirano nijedno bolovanje na teret Fonda."
                : $"{Mesec:D2}/{Godina}: {_spisak.Redovi.Count} osiguranika · za refundaciju {_spisak.UkupnoZaIsplatu:N2}";
        }
        catch (Exception ex)
        {
            StatusMessage.Text = $"Greška pri učitavanju: {ex.Message}";
        }
    }

    /// <summary>Zaposleni izabranog perioda; lica van radnog odnosa nemaju bolovanje.</summary>
    private void PopuniRadnike()
    {
        int izabrani = (ComboRadnik.SelectedItem as RadnikStavka)?.BrojRadnika ?? 0;

        var radnici = _db.Radnici
            .Where(r => r.Godina == Godina && r.Mesec == Mesec && !r.VanRadnogOdnosa)
            .OrderBy(r => r.BrojRadnika)
            .Select(r => new RadnikStavka { BrojRadnika = r.BrojRadnika, ImeIPrezime = r.ImeIPrezime })
            .ToList();

        ComboRadnik.ItemsSource = radnici;
        ComboRadnik.SelectedItem = radnici.FirstOrDefault(r => r.BrojRadnika == izabrani) ?? radnici.FirstOrDefault();
    }

    /// <summary>Predlog perioda je ceo izabrani mesec — najčešći slučaj kod bolovanja preko 30 dana.</summary>
    private void PredlozeniDatumi()
    {
        if (DatumOd.SelectedDate.HasValue) return;

        var prvi = new DateTime(Godina, Mesec, 1);
        DatumOd.SelectedDate = prvi;
        DatumDo.SelectedDate = prvi.AddMonths(1).AddDays(-1);
        DatumPocetka.SelectedDate = prvi.AddDays(-30);
    }

    // ── Unos ─────────────────────────────────────────────────────────────

    private void BtnDodaj_Click(object sender, RoutedEventArgs e)
    {
        if (ComboRadnik.SelectedItem is not RadnikStavka radnik)
        {
            MessageBox.Show("Izaberite radnika.", "Nema selekcije", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (DatumOd.SelectedDate is not DateTime od || DatumDo.SelectedDate is not DateTime doDatum)
        {
            MessageBox.Show("Unesite period za koji se traži refundacija.", "Nepotpun unos",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (doDatum < od)
        {
            MessageBox.Show("Datum „do“ je pre datuma „od“.", "Obrnut period",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var pocetak = DatumPocetka.SelectedDate ?? od;

        if (pocetak > od)
        {
            MessageBox.Show("Početak sprečenosti je posle prvog dana refundacije.", "Neispravni datumi",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        bool vecPostoji = _db.Bolovanja.Any(b =>
            b.BrojRadnika == radnik.BrojRadnika && b.Godina == Godina && b.Mesec == Mesec && b.DatumOd == od);

        if (vecPostoji)
        {
            MessageBox.Show(
                "Za tog radnika u ovom mesecu već postoji bolovanje sa istim prvim danom.\n\n" +
                "Dva zapisa istog perioda značila bi dva zahteva Fondu za isti novac.",
                "Zapis već postoji", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            _db.Bolovanja.Add(new Bolovanje
            {
                BrojRadnika = radnik.BrojRadnika,
                Godina = Godina,
                Mesec = Mesec,
                DatumPocetkaSprecenosti = pocetak,
                DatumOd = od,
                DatumDo = doDatum,
                Osnov = (ComboOsnov.SelectedItem as OsnovStavka)?.Osnov ?? OsnovSprecenosti.Bolest,
                PrvaIsplata = ChkPrvaIsplata.IsChecked == true
            });

            _db.SaveChanges();

            AuditService.Zabelezi(_db, Godina, Mesec, AkcijaObracuna.BolovanjeEvidentirano,
                $"Evidentirano bolovanje: {radnik.ImeIPrezime}, {od:dd.MM.yyyy}–{doDatum:dd.MM.yyyy}");

            Ucitaj();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Bolovanje nije sačuvano:\n\n{ex.Message}", "Greška",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnObrisi_Click(object sender, RoutedEventArgs e)
    {
        if (GridRedovi.SelectedItem is not Oz10Red red)
        {
            MessageBox.Show("Izaberite red u tabeli.", "Nema selekcije", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (MessageBox.Show(
                $"Obrisati bolovanje za {red.Radnik.ImeIPrezime} ({red.Bolovanje.PeriodStr})?\n\n" +
                "Briše se samo evidencija — obračun i isplaćena naknada ostaju netaknuti.",
                "Potvrda brisanja", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        try
        {
            var zapis = _db.Bolovanja.FirstOrDefault(b => b.BolovanjeId == red.Bolovanje.BolovanjeId);
            if (zapis == null) return;

            _db.Bolovanja.Remove(zapis);
            _db.SaveChanges();

            AuditService.Zabelezi(_db, Godina, Mesec, AkcijaObracuna.BolovanjeEvidentirano,
                $"Obrisano bolovanje: {red.Radnik.ImeIPrezime}, {red.Bolovanje.PeriodStr}");

            Ucitaj();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Bolovanje nije obrisano:\n\n{ex.Message}", "Greška",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ── Obrasci ──────────────────────────────────────────────────────────

    private Firma? Firma() => _db.Firme.FirstOrDefault();

    /// <summary>
    /// Nalazi ne blokiraju štampu — obrazac se ponekad priprema dok se podatak pribavlja —
    /// ali se traži izričita potvrda, jer filijala nepotpun obrazac vraća.
    /// </summary>
    private bool PotvrdiUprkosNalazima(IReadOnlyList<NalazProvere> nalazi)
    {
        int gresaka = nalazi.Count(n => n.Tezina == TezinaNalaza.Greska);
        if (gresaka == 0) return true;

        return MessageBox.Show(
            $"Kontrole su našle {gresaka} grešaka.\n\n" +
            "Obrazac sa nepotpunim podacima filijala vraća, a refundacija se odlaže za ceo mesec.\n\n" +
            "Želite li ipak da nastavite?",
            "Kontrole nisu prošle", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
    }

    private void BtnOz7_Click(object sender, RoutedEventArgs e)
    {
        if (GridRedovi.SelectedItem is not Oz10Red red)
        {
            MessageBox.Show("Izaberite bolovanje u tabeli.", "Nema selekcije",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var (obrazac, nalazi) = _servis.PripremiOz7(red.Bolovanje.BolovanjeId);

        GridNalazi.ItemsSource = nalazi;

        if (obrazac == null)
        {
            MessageBox.Show("Obrazac se ne može sastaviti — pogledajte kontrolne provere.", "OZ-7",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!PotvrdiUprkosNalazima(nalazi)) return;

        var sfd = new SaveFileDialog
        {
            Filter = "PDF dokument (*.pdf)|*.pdf",
            FileName = $"OZ-7_{BezbednoIme(obrazac.Radnik.ImeIPrezime)}_{Mesec:D2}-{Godina}.pdf",
            Title = "Sačuvaj obrazac OZ-7"
        };

        if (sfd.ShowDialog() != true) return;

        Izvrsi(() =>
        {
            Oz7Document.Sacuvaj(obrazac, Firma(), sfd.FileName);
            StatusMessage.Text = $"Sačuvano: {sfd.FileName} · prosek po času (bruto) {obrazac.ProsekBrutoPoCasu:N4}";
        });
    }

    private void BtnOz10_Click(object sender, RoutedEventArgs e)
    {
        if (_spisak == null || _spisak.Redovi.Count == 0)
        {
            MessageBox.Show("Za izabrani mesec nema evidentiranih bolovanja.", "OZ-10",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!PotvrdiUprkosNalazima(_spisak.Nalazi)) return;

        var sfd = new SaveFileDialog
        {
            Filter = "PDF dokument (*.pdf)|*.pdf",
            FileName = $"OZ-10_{Mesec:D2}-{Godina}.pdf",
            Title = "Sačuvaj obrazac OZ-10"
        };

        if (sfd.ShowDialog() != true) return;

        Izvrsi(() =>
        {
            Oz10Document.Sacuvaj(_spisak, Firma(), sfd.FileName);

            AuditService.Zabelezi(_db, Godina, Mesec, AkcijaObracuna.ObrazacRfzo,
                $"Izdat obrazac OZ-10 za {Mesec:D2}/{Godina}: {_spisak.Redovi.Count} osiguranika, " +
                $"za refundaciju {_spisak.UkupnoZaIsplatu:N2}");

            StatusMessage.Text = $"Sačuvano: {sfd.FileName} · za refundaciju {_spisak.UkupnoZaIsplatu:N2}";
        });
    }

    private static void Izvrsi(Action akcija)
    {
        try
        {
            akcija();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Dokument nije sačuvan:\n\n{ex.Message}", "Greška",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static string BezbednoIme(string ime)
    {
        foreach (char nedozvoljen in Path.GetInvalidFileNameChars())
            ime = ime.Replace(nedozvoljen, '_');
        return ime.Replace(' ', '_');
    }
}
