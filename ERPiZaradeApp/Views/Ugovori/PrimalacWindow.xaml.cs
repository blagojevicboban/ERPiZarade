using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ERPiZaradeApp.Services;
using ERPiZaradeData;
using ERPiZaradeData.Models;

namespace ERPiZaradeApp.Views.Ugovori;

/// <summary>
/// Unos primaoca po ugovoru (Faza 2.3).
///
/// Primalac je karton radnika sa oznakom <see cref="Radnik.VanRadnogOdnosa"/>. Oznaka se može
/// postaviti i u „Radnici", ali tek pošto se karton otvori za izmenu — a to je za jedan
/// čekboks previše koraka, i previše prilika da se zaboravi „Sačuvaj". Zato se primalac unosi
/// odavde, sa ekrana na kom i treba.
/// </summary>
public partial class PrimalacWindow : Window
{
    private readonly PlataDbContext _db;
    private readonly int _godina;
    private readonly int _mesec;
    private List<Radnik> _kandidati = [];

    /// <summary>Broj radnika koji je upravo unet ili označen; nula kad ništa nije urađeno.</summary>
    public int BrojRadnika { get; private set; }

    public PrimalacWindow(int godina, int mesec)
    {
        InitializeComponent();

        _godina = godina;
        _mesec = mesec;
        _db = PlataDbContext.Create(AppConfig.DbPath);

        UcitajKandidate();
        StatusMessage.Text = $"Karton se pravi za period {_mesec:D2}/{_godina}.";
    }

    /// <summary>
    /// Kartoni koji još nisu označeni. Uzima se poslednji zapis svakog lica — karton je
    /// periodičan, a lice je jedno.
    /// </summary>
    private void UcitajKandidate()
    {
        _kandidati = _db.Radnici
            .Where(r => !r.VanRadnogOdnosa)
            .OrderByDescending(r => r.Godina).ThenByDescending(r => r.Mesec)
            .ToList()
            .GroupBy(r => r.BrojRadnika)
            .Select(g => g.First())
            .OrderBy(r => r.BrojRadnika)
            .ToList();

        Filtriraj();
    }

    private void TxtPretraga_TextChanged(object sender, TextChangedEventArgs e) => Filtriraj();

    private void Filtriraj()
    {
        string trazi = TxtPretraga.Text.Trim();

        var prikaz = trazi.Length == 0
            ? _kandidati
            : _kandidati.Where(r =>
                r.ImeIPrezime.Contains(trazi, StringComparison.OrdinalIgnoreCase)
                || r.Jmbg.Contains(trazi, StringComparison.Ordinal)
                || r.BrojRadnika.ToString().Contains(trazi, StringComparison.Ordinal)).ToList();

        // Spisak zna da bude veliki; prikazuje se koliko je dovoljno za prepoznavanje.
        GridRadnici.ItemsSource = prikaz.Take(300).ToList();
    }

    // ── Novi primalac ────────────────────────────────────────────────

    private void BtnDodaj_Click(object sender, RoutedEventArgs e)
    {
        string ime = TxtIme.Text.Trim();
        if (ime.Length == 0)
        {
            StatusMessage.Text = "Unesite ime i prezime primaoca.";
            return;
        }

        string jmbg = TxtJmbg.Text.Trim();

        // JMBG nije obavezan za unos kartona, ali bez njega primalac ispada iz prijave —
        // pa se na to upozorava odmah, a ne tek pre podnošenja.
        if (jmbg.Length > 0 && !JmbgValidator.Validate(jmbg, out string greska))
        {
            if (MessageBox.Show($"{greska}\n\nSvejedno sačuvati?", "Neispravan JMBG",
                    MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            {
                return;
            }
        }

        try
        {
            int sledeciBroj = (_db.Radnici.Select(r => (int?)r.BrojRadnika).Max() ?? 0) + 1;

            var karton = new Radnik
            {
                Godina = _godina,
                Mesec = _mesec,
                BrojRadnika = sledeciBroj,
                ImeIPrezime = ime,
                Jmbg = jmbg,
                AdresaStanovanja = TxtAdresa.Text.Trim(),
                Mesto = TxtMesto.Text.Trim(),
                SifraOpstine = TxtOpstina.Text.Trim(),
                BankovniRacun = TxtRacun.Text.Trim(),
                Email = TxtEmail.Text.Trim(),
                Aktivan = true,
                VanRadnogOdnosa = true,
                DatumUnosa = DateTime.Now
            };

            _db.Radnici.Add(karton);
            _db.SaveChanges();

            BrojRadnika = karton.BrojRadnika;
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Primalac nije dodat: {ex.Message}", "Greška",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ── Označavanje postojećeg kartona ───────────────────────────────

    /// <summary>
    /// Uzima izabrano lice za primaoca. Oznaka <see cref="Radnik.VanRadnogOdnosa"/> se pri tom
    /// postavlja <b>samo licu koje nije zaposleno</b>.
    ///
    /// Zaposleni sme biti isplaćen po ugovoru — šifra vrste prihoda za to je
    /// <c>1 01 601 00 0</c>. Da mu se pri izboru postavi oznaka, nestao bi iz obračuna plate,
    /// radnih sati i platnih listića, a to nije ono što je korisnik tražio: on je hteo da mu
    /// obračuna honorar, ne da ga skine sa platnog spiska. Da je lice primalac beleži sam
    /// ugovor (<see cref="Ugovor.BrojRadnika"/>), pa oznaka za to nije ni potrebna.
    /// </summary>
    private void BtnOznaci_Click(object sender, RoutedEventArgs e)
    {
        if (GridRadnici.SelectedItem is not Radnik izabrani)
        {
            StatusMessage.Text = "Izaberite karton koji želite da označite.";
            return;
        }

        // Aktivan karton bez oznake je lice u radnom odnosu. Njemu se ništa ne menja.
        bool uRadnomOdnosu = izabrani.Aktivan;

        string pitanje = uRadnomOdnosu
            ? $"Uzeti „{izabrani.ImeIPrezime}\" za primaoca po ugovoru?\n\n" +
              "Lice je u radnom odnosu, pa mu se u kartonu NIŠTA ne menja — i dalje se obračunava " +
              "zarada, radni sati i platni listić. Uz ugovor izaberite tip primaoca „01 — zaposleni“."
            : $"Označiti „{izabrani.ImeIPrezime}\" kao lice van radnog odnosa?\n\n" +
              "Posle toga ga ekrani zarade neće nuditi za obračun plate, radne sate ni platni listić. " +
              "Već obračunate zarade ostaju netaknute.";

        if (MessageBox.Show(pitanje, "Potvrda", MessageBoxButton.YesNo, MessageBoxImage.Question)
            != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            if (!uRadnomOdnosu)
            {
                // Oznaka se postavlja na SVE periode tog lica: karton je periodičan, a to da
                // neko nije u radnom odnosu nije svojstvo meseca. Da se postavi samo na jedan,
                // ekrani zarade bi ga u ostalim mesecima i dalje nudili.
                var svi = _db.Radnici.Where(r => r.BrojRadnika == izabrani.BrojRadnika).ToList();
                foreach (var r in svi) r.VanRadnogOdnosa = true;

                _db.SaveChanges();
            }

            BrojRadnika = izabrani.BrojRadnika;
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Karton nije označen: {ex.Message}", "Greška",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
