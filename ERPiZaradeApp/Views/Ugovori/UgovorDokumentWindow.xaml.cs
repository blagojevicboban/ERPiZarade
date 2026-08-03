using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using QuestPDF.Fluent;
using ERPiZaradeApp.Services;
using ERPiZaradeData;
using ERPiZaradeData.Models;

namespace ERPiZaradeApp.Views.Ugovori;

/// <summary>
/// Tekst zaključenog ugovora — generisanje iz šablona, ručno uređivanje i izvoz u PDF
/// (Faza 2.3).
///
/// Tekst se čuva uz ugovor, a ne uz šablon: šablon se s vremenom menja, a potpisan ugovor
/// mora ostati onakav kakav je potpisan. Zato ponovno generisanje traži potvrdu — ono briše
/// sve što je posle prvog generisanja dopisano rukom.
/// </summary>
public partial class UgovorDokumentWindow : Window
{
    private readonly PlataDbContext _db;
    private readonly UgovorTekstService _servis;
    private readonly int _ugovorId;
    private bool _izmenjeno;

    public UgovorDokumentWindow(int ugovorId)
    {
        InitializeComponent();

        _ugovorId = ugovorId;
        _db = PlataDbContext.Create(AppConfig.DbPath);
        _servis = new UgovorTekstService(_db);

        GridPolja.ItemsSource = UgovorTekstService.Polja
            .Select(p => new { p.Polje, p.Opis })
            .ToList();

        Ucitaj();
    }

    private Ugovor? Ugovor => _db.Ugovori
        .Include(u => u.VrstaUgovora)
        .FirstOrDefault(u => u.UgovorId == _ugovorId);

    private void Ucitaj()
    {
        var ugovor = Ugovor;
        if (ugovor == null)
        {
            StatusMessage.Text = "Ugovor nije pronađen.";
            return;
        }

        var sabloni = _db.SabloniUgovora
            .Where(s => s.Aktivan)
            .OrderBy(s => s.Redosled)
            .ToList();

        ComboSablon.ItemsSource = sabloni;
        ComboSablon.SelectedItem = _servis.PodrazumevaniSablon(ugovor) is { } podrazumevani
            ? sabloni.FirstOrDefault(s => s.SablonUgovoraId == podrazumevani.SablonUgovoraId)
            : sabloni.FirstOrDefault();

        string primalac = _db.Radnici
            .Where(r => r.BrojRadnika == ugovor.BrojRadnika)
            .OrderByDescending(r => r.Godina).ThenByDescending(r => r.Mesec)
            .Select(r => r.ImeIPrezime)
            .FirstOrDefault() ?? $"#{ugovor.BrojRadnika}";

        Title = $"Tekst ugovora — {primalac}";
        TxtZaglavlje.Text = $"{ugovor.VrstaUgovora?.Naziv} · {primalac} · " +
                            $"{ugovor.UgovorenIznos:N2} {(ugovor.IznosJeNeto ? "neto" : "bruto")}";

        TxtTekst.Text = ugovor.Tekst;
        _izmenjeno = false;

        StatusMessage.Text = string.IsNullOrWhiteSpace(ugovor.Tekst)
            ? "Ugovor još nema tekst. Izaberite šablon i pritisnite „Generiši iz šablona\"."
            : $"Tekst je poslednji put sačuvan {ugovor.DatumTeksta:dd.MM.yyyy HH:mm}.";
    }

    private void TxtTekst_TextChanged(object sender, TextChangedEventArgs e) => _izmenjeno = true;

    private void BtnGenerisi_Click(object sender, RoutedEventArgs e)
    {
        if (ComboSablon.SelectedItem is not SablonUgovora sablon)
        {
            StatusMessage.Text = "Izaberite šablon.";
            return;
        }

        // Ručne izmene su ono zbog čega editor i postoji; ne brišu se bez pitanja.
        if (TxtTekst.Text.Trim().Length > 0 &&
            MessageBox.Show(
                "Generisanje prepisuje zatečeni tekst, uključujući sve što je dopisano rukom.\n\nNastaviti?",
                "Prepisivanje teksta", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        var rezultat = _servis.Generisi(_ugovorId, sablon.SablonUgovoraId);
        StatusMessage.Text = rezultat.Poruka;

        if (!rezultat.Uspesno)
        {
            MessageBox.Show(rezultat.Poruka, "Tekst nije generisan",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        TxtTekst.Text = rezultat.Tekst;

        if (rezultat.NepopunjenaPolja.Count > 0)
        {
            MessageBox.Show(
                "Tekst je generisan, ali sledeća polja nisu popunjena i ostala su vidljiva u dokumentu:\n\n" +
                string.Join("\n", rezultat.NepopunjenaPolja.Select(p => "• " + p)) +
                "\n\nDopunite ih u kartonu firme, kartonu primaoca ili u samom ugovoru, pa generišite ponovo — " +
                "ili ih ispravite ovde rukom.",
                "Nepopunjena polja", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void BtnSacuvaj_Click(object sender, RoutedEventArgs e)
    {
        var rezultat = _servis.Sacuvaj(_ugovorId, TxtTekst.Text);
        StatusMessage.Text = rezultat.Poruka;
        _izmenjeno = !rezultat.Uspesno;
    }

    private void BtnPdf_Click(object sender, RoutedEventArgs e)
    {
        if (TxtTekst.Text.Trim().Length == 0)
        {
            StatusMessage.Text = "Nema teksta za izvoz.";
            return;
        }

        var ugovor = Ugovor;

        var dijalog = new SaveFileDialog
        {
            Filter = "PDF dokument (*.pdf)|*.pdf",
            FileName = ImeFajla(ugovor),
            Title = "Snimi ugovor kao PDF"
        };

        if (dijalog.ShowDialog() != true) return;

        try
        {
            string podnozje = ugovor?.VrstaUgovora?.Naziv ?? "";
            if (!string.IsNullOrWhiteSpace(ugovor?.Broj)) podnozje += $" br. {ugovor.Broj}";

            Document.Create(container => container.Page(page =>
                    new Views.Stampe.UgovorDocument(TxtTekst.Text, podnozje).Build(page)))
                .GeneratePdf(dijalog.FileName);

            StatusMessage.Text = $"PDF je snimljen: {dijalog.FileName}";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"PDF nije napravljen: {ex.Message}", "Greška",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private string ImeFajla(Ugovor? ugovor)
    {
        string primalac = _db.Radnici
            .Where(r => ugovor != null && r.BrojRadnika == ugovor.BrojRadnika)
            .OrderByDescending(r => r.Godina).ThenByDescending(r => r.Mesec)
            .Select(r => r.ImeIPrezime)
            .FirstOrDefault() ?? "";

        string osnova = $"Ugovor {ugovor?.Broj} {primalac}".Trim();
        foreach (char c in Path.GetInvalidFileNameChars()) osnova = osnova.Replace(c, '_');

        return (osnova.Length == 0 ? "Ugovor" : osnova) + ".pdf";
    }

    private void GridPolja_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (GridPolja.SelectedItem == null) return;

        string polje = GridPolja.SelectedItem.GetType().GetProperty("Polje")?
            .GetValue(GridPolja.SelectedItem)?.ToString() ?? "";

        if (polje.Length == 0) return;

        int mesto = TxtTekst.CaretIndex;
        TxtTekst.Text = TxtTekst.Text.Insert(mesto, polje);
        TxtTekst.CaretIndex = mesto + polje.Length;
        TxtTekst.Focus();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (_izmenjeno)
        {
            var odgovor = MessageBox.Show(
                "Tekst je izmenjen a nije sačuvan. Sačuvati pre zatvaranja?",
                "Nesačuvane izmene", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

            if (odgovor == MessageBoxResult.Cancel) { e.Cancel = true; return; }
            if (odgovor == MessageBoxResult.Yes) _servis.Sacuvaj(_ugovorId, TxtTekst.Text);
        }

        base.OnClosing(e);
    }
}
