using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ERPiZaradeData;
using ERPiZaradeData.Models;

namespace ERPiZaradeApp.Views.VrstePrimanja;

public partial class VrstePrimanjaPage : Page
{
    private PlataDbContext _db;
    private ObservableCollection<VrstaPrimanja> _vrste = [];
    private readonly List<VrstaPrimanja> _zaBrisanje = [];

    public VrstePrimanjaPage()
    {
        InitializeComponent();
        _db = PlataDbContext.Create(AppConfig.DbPath);
        Ucitaj();
    }

    private void Ucitaj()
    {
        _zaBrisanje.Clear();
        _vrste = new ObservableCollection<VrstaPrimanja>(
            _db.VrstePrimanja.OrderBy(v => v.Redosled).ThenBy(v => v.Sifra));

        GridVrste.ItemsSource = _vrste;
        StatusMessage.Text = $"{_vrste.Count} vrsta primanja ({_vrste.Count(v => v.JeSistemska)} sistemskih).";
    }

    /// <summary>
    /// Prevodi zatečene obračune na model stavki. Proba se izvršava uvek pre upisa — radi
    /// se nad podacima koji su već isplaćeni, pa se ne upisuje ništa što korisnik nije video.
    /// </summary>
    private void BtnPrevedi_Click(object sender, RoutedEventArgs e)
    {
        var servis = new Services.PrevodStavkiService(_db);

        Services.PrevodRezultat proba;
        try
        {
            proba = servis.Proveri();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Proba nije izvršena: {ex.Message}", "Greška",
                MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        if (proba.Prevedeno == 0 && proba.JeCist)
        {
            MessageBox.Show(
                proba.VecImajuStavke > 0
                    ? $"Svi obračuni ({proba.VecImajuStavke}) već imaju stavke."
                    : "Nema obračuna za prevođenje.",
                "Nema šta da se prevede", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        string poruka =
            $"Obračuna ukupno: {proba.UkupnoObracuna}\n" +
            $"Već imaju stavke: {proba.VecImajuStavke}\n" +
            $"Biće prevedeno: {proba.Prevedeno}\n";

        if (!proba.JeCist)
        {
            poruka += $"\nNeće biti prevedeno (zbir stavki se ne slaže sa bruto iznosom): {proba.Neslaganja.Count}\n\n" +
                      string.Join(Environment.NewLine, proba.Neslaganja.Take(10)
                          .Select(n => $"• {n.Mesec:D2}/{n.Godina} {n.BrojRadnika} {n.Radnik} — razlika {n.Razlika:N2}"));

            if (proba.Neslaganja.Count > 10)
                poruka += $"{Environment.NewLine}… i još {proba.Neslaganja.Count - 10}.";

            poruka += "\n\nTi obračuni ostaju nepromenjeni i mogu se prevesti kasnije, kad se utvrdi odakle razlika.";
        }

        poruka += "\n\nNastaviti sa upisom?";

        if (MessageBox.Show(poruka, "Prevođenje na model stavki",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            var rezultat = servis.Prevedi();

            StatusMessage.Text = $"Prevedeno {rezultat.Prevedeno} obračuna; neslaganja: {rezultat.Neslaganja.Count}.";
            MessageBox.Show(
                $"Prevedeno je {rezultat.Prevedeno} obračuna." +
                (rezultat.JeCist ? "" : $"\n\n{rezultat.Neslaganja.Count} obračuna je ostalo nepromenjeno."),
                "Prevođenje završeno", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Prevođenje nije izvršeno: {ex.Message}", "Greška",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnDodaj_Click(object sender, RoutedEventArgs e)
    {
        var nova = new VrstaPrimanja
        {
            Sifra = "",
            Naziv = "",
            Oporezivo = true,
            UlaziUOsnovicuDoprinosa = true,
            Aktivna = true,
            JeSistemska = false,
            Redosled = (_vrste.Count == 0 ? 0 : _vrste.Max(v => v.Redosled)) + 10
        };

        _vrste.Add(nova);
        GridVrste.SelectedItem = nova;
        GridVrste.ScrollIntoView(nova);
        StatusMessage.Text = "Unesite šifru i naziv, pa pritisnite „Sačuvaj\".";
    }

    private void BtnObrisi_Click(object sender, RoutedEventArgs e)
    {
        if (GridVrste.SelectedItem is not VrstaPrimanja izabrana)
        {
            StatusMessage.Text = "Izaberite vrstu koju želite da obrišete.";
            return;
        }

        // Sistemske vrste popunjava obračun i traži ih po šifri — brisanje bi ostavilo
        // stavke bez vrste i razišlo zbir sa bruto iznosom.
        if (izabrana.JeSistemska)
        {
            MessageBox.Show(
                $"„{izabrana.Naziv}\" je sistemska vrsta koju popunjava sam obračun i ne može se obrisati.\n\n" +
                "Ako je ne koristite, isključite je poljem „Aktivna\".",
                "Sistemska vrsta", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (izabrana.VrstaPrimanjaId != 0
            && _db.ObracunStavke.Any(s => s.VrstaPrimanjaId == izabrana.VrstaPrimanjaId))
        {
            MessageBox.Show(
                $"„{izabrana.Naziv}\" je upotrebljena u postojećim obračunima i ne može se obrisati.\n\n" +
                "Isključite je poljem „Aktivna\" da se više ne nudi.",
                "Vrsta je u upotrebi", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _vrste.Remove(izabrana);
        if (izabrana.VrstaPrimanjaId != 0) _zaBrisanje.Add(izabrana);

        StatusMessage.Text = $"„{izabrana.Naziv}\" će biti obrisana po snimanju.";
    }

    private void BtnSacuvaj_Click(object sender, RoutedEventArgs e)
    {
        GridVrste.CommitEdit(DataGridEditingUnit.Row, true);

        if (_vrste.Any(v => string.IsNullOrWhiteSpace(v.Sifra) || string.IsNullOrWhiteSpace(v.Naziv)))
        {
            MessageBox.Show("Svaka vrsta mora imati šifru i naziv.", "Nepotpun unos",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Šifra je ono po čemu kod traži vrstu, pa dve iste čine šifarnik dvosmislenim.
        var duplikat = _vrste
            .GroupBy(v => v.Sifra.Trim(), StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(g => g.Count() > 1);

        if (duplikat != null)
        {
            MessageBox.Show($"Šifra „{duplikat.Key}\" je upotrebljena više puta.", "Dvostruka šifra",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var neispravanSvp = _vrste.FirstOrDefault(v =>
            !string.IsNullOrWhiteSpace(v.Svp) && !Services.SvpService.JeSvpSifra(v.Svp.Trim()));

        if (neispravanSvp != null)
        {
            MessageBox.Show(
                $"SVP šifra kod „{neispravanSvp.Naziv}\" nije ispravna — mora imati tačno devet cifara ili biti prazna.",
                "Neispravna SVP šifra", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            if (_zaBrisanje.Count > 0) _db.VrstePrimanja.RemoveRange(_zaBrisanje);

            foreach (var vrsta in _vrste)
            {
                vrsta.Sifra = vrsta.Sifra.Trim();
                vrsta.Svp = (vrsta.Svp ?? "").Trim();

                if (vrsta.VrstaPrimanjaId == 0) _db.VrstePrimanja.Add(vrsta);
            }

            _db.SaveChanges();

            _db = PlataDbContext.Create(AppConfig.DbPath);
            Ucitaj();

            StatusMessage.Text = "Šifarnik je sačuvan. Izmene važe od sledećeg obračuna.";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Šifarnik nije sačuvan: {ex.Message}", "Greška",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
