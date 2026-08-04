using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;
using ERPiZaradeApp.Services;
using ERPiZaradeData;
using ERPiZaradeData.Models;

namespace ERPiZaradeApp.Views.Ugovori;

/// <summary>Red na spisku primalaca — lice sa zbirom onoga što mu je isplaćeno po ugovorima.</summary>
public sealed class PrimalacRed
{
    public int BrojRadnika { get; init; }
    public string ImeIPrezime { get; init; } = "";
    public string Jmbg { get; init; } = "";
    public string BankovniRacun { get; init; } = "";
    public string Status { get; init; } = "";
    public int BrojUgovora { get; init; }
    public int BrojIsplata { get; init; }
    public decimal IsplacenoBruto { get; init; }
}

/// <summary>
/// Evidencija lica kojima se isplaćuje po ugovorima van radnog odnosa.
///
/// Ovo je <b>pogled nad registrom `Radnici`, a ne zaseban registar</b>. Lice u radnom odnosu
/// sme biti isplaćeno i po ugovoru — šifra vrste prihoda za to je <c>1 01 601 00 0</c>, gde
/// <c>01</c> znači „zaposleni". Da su primaoci zasebna tabela, takvo lice bi u bazi stajalo
/// dvaput, a <see cref="PppPoService"/> grupiše po <c>BrojRadnika</c> kroz sve obračune — pa
/// bi mu izdao <b>dve</b> godišnje potvrde umesto jedne.
///
/// Ko je primalac zato se izvodi iz <see cref="Ugovor"/>, a ne iz oznake u kartonu:
/// <see cref="Radnik.VanRadnogOdnosa"/> kaže samo to da lice <b>nije u radnom odnosu</b>.
/// </summary>
public partial class PrimaociPage : Page
{
    private readonly PlataDbContext _db;

    public PrimaociPage()
    {
        InitializeComponent();
        _db = PlataDbContext.Create(AppConfig.DbPath);
        Ucitaj();
    }

    private void BtnOsvezi_Click(object sender, RoutedEventArgs e) => Ucitaj();

    private void BtnNovi_Click(object sender, RoutedEventArgs e)
    {
        var prozor = new PrimalacWindow(AppConfig.ActiveGodina ?? DateTime.Now.Year,
                                        AppConfig.ActiveMesec ?? DateTime.Now.Month)
        {
            Owner = Window.GetWindow(this)
        };

        if (prozor.ShowDialog() == true) Ucitaj();
    }

    private void Ucitaj()
    {
        try
        {
            // Spisak čine lica koja imaju ugovor i lica označena kao van radnog odnosa. Prvo
            // obuhvata i zaposlene sa honorarom, drugo one koji su uneti a ugovor još nemaju.
            var brojeviIzUgovora = _db.Ugovori.Select(u => u.BrojRadnika).Distinct().ToList();

            var oznaceni = _db.Radnici
                .Where(r => r.VanRadnogOdnosa)
                .Select(r => r.BrojRadnika)
                .Distinct()
                .ToList();

            var brojevi = brojeviIzUgovora.Union(oznaceni).ToHashSet();

            if (brojevi.Count == 0)
            {
                GridPrimaoci.ItemsSource = null;
                StatusMessage.Text = "Nema nijednog primaoca. Dodajte ga dugmetom ➕ ili sa ekrana „Ugovori i naknade“.";
                return;
            }

            // Karton je periodičan — uzima se poslednji zapis svakog lica.
            var kartoni = _db.Radnici
                .Where(r => brojevi.Contains(r.BrojRadnika))
                .OrderByDescending(r => r.Godina).ThenByDescending(r => r.Mesec)
                .ToList()
                .GroupBy(r => r.BrojRadnika)
                .ToDictionary(g => g.Key, g => g.First());

            int ugovoraPoLicu(int broj) => _db.Ugovori.Count(u => u.BrojRadnika == broj);

            // Zbrajanje ide u memoriji, posle ToList(): SQLite ne ume SUM nad decimal kolonom.
            var naknade = _db.ObracuniPlata
                .AsNoTracking()
                .Where(o => o.UgovorId != null && !o.Storniran)
                .Include(o => o.Ugovor)
                .Select(o => new { o.Ugovor!.BrojRadnika, o.BrutoZarada })
                .ToList()
                .GroupBy(o => o.BrojRadnika)
                .ToDictionary(g => g.Key, g => (Broj: g.Count(), Bruto: g.Sum(o => o.BrutoZarada)));

            var redovi = new List<PrimalacRed>();

            foreach (int broj in brojevi.OrderBy(b => b))
            {
                if (!kartoni.TryGetValue(broj, out var karton)) continue;

                naknade.TryGetValue(broj, out var zbir);

                redovi.Add(new PrimalacRed
                {
                    BrojRadnika = broj,
                    ImeIPrezime = karton.ImeIPrezime,
                    Jmbg = karton.Jmbg,
                    BankovniRacun = karton.BankovniRacun,
                    Status = karton.VanRadnogOdnosa
                        ? "van radnog odnosa"
                        : karton.Aktivan ? "i u radnom odnosu" : "bivši zaposleni",
                    BrojUgovora = ugovoraPoLicu(broj),
                    BrojIsplata = zbir.Broj,
                    IsplacenoBruto = zbir.Bruto
                });
            }

            GridPrimaoci.ItemsSource = redovi;

            int uRadnomOdnosu = redovi.Count(r => r.Status == "i u radnom odnosu");

            StatusMessage.Text = uRadnomOdnosu > 0
                ? $"{redovi.Count} primalaca, od toga {uRadnomOdnosu} i u radnom odnosu — njima naknada " +
                  "ide uz tip primaoca 01, a zarada im ostaje netaknuta."
                : $"{redovi.Count} primalaca.";
        }
        catch (Exception ex)
        {
            StatusMessage.Text = $"Greška pri učitavanju: {ex.Message}";
        }
    }
}
