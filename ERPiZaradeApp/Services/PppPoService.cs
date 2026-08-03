using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using ERPiZaradeData;
using ERPiZaradeData.Models;

namespace ERPiZaradeApp.Services;

/// <summary>Jedan red obrasca — zbir po vrsti prihoda za jednog radnika u godini.</summary>
public sealed class PppPoRed
{
    public required string Svp { get; init; }

    /// <summary>Bruto prihod isplaćen po toj vrsti prihoda.</summary>
    public decimal BrutoPrihod { get; set; }

    /// <summary>Osnovica na koju je obračunat porez.</summary>
    public decimal PoreskaOsnovica { get; set; }

    public decimal Porez { get; set; }
    public decimal DoprinosPio { get; set; }
    public decimal DoprinosZdravstvo { get; set; }
    public decimal DoprinosNezaposlenost { get; set; }

    public decimal UkupnoDoprinosi => DoprinosPio + DoprinosZdravstvo + DoprinosNezaposlenost;

    /// <summary>Meseci u kojima je bilo isplate po ovoj vrsti prihoda.</summary>
    public SortedSet<int> Meseci { get; } = [];
}

/// <summary>Obrazac za jednog radnika.</summary>
public sealed class PppPoObrazac
{
    public required int Godina { get; init; }
    public required Radnik Radnik { get; init; }
    public required IReadOnlyList<PppPoRed> Redovi { get; init; }

    public decimal UkupnoBruto => Redovi.Sum(r => r.BrutoPrihod);
    public decimal UkupnoOsnovica => Redovi.Sum(r => r.PoreskaOsnovica);
    public decimal UkupnoPorez => Redovi.Sum(r => r.Porez);
    public decimal UkupnoDoprinosi => Redovi.Sum(r => r.UkupnoDoprinosi);

    public int BrojMeseci => Redovi.SelectMany(r => r.Meseci).Distinct().Count();
}

/// <summary>Rezultat pripreme za celu godinu, sa kontrolama.</summary>
public sealed class PppPoRezultat
{
    public required int Godina { get; init; }
    public IReadOnlyList<PppPoObrazac> Obrasci { get; init; } = [];
    public IReadOnlyList<NalazProvere> Nalazi { get; init; } = [];

    public decimal UkupnoPorez => Obrasci.Sum(o => o.UkupnoPorez);
    public decimal UkupnoDoprinosi => Obrasci.Sum(o => o.UkupnoDoprinosi);

    public int BrojGresaka => Nalazi.Count(n => n.Tezina == TezinaNalaza.Greska);
}

/// <summary>
/// Godišnji obrazac <b>PPP-PO</b> — potvrda o plaćenim porezima i doprinosima po odbitku,
/// koju je poslodavac dužan da uruči radniku do 31. januara za prethodnu godinu.
///
/// Sastavlja se iz obračuna cele godine, grupisano po vrsti prihoda (SVP). Zbir poreza i
/// doprinosa mora da se slaže sa onim što je prijavljeno kroz PPP-PD — razlika znači da je
/// neki obračun izmenjen posle podnošenja prijave, i prijavljuje se pre štampe.
/// </summary>
public class PppPoService
{
    private readonly PlataDbContext _db;

    public PppPoService(PlataDbContext db) => _db = db;

    public PppPoRezultat Pripremi(int godina, int? samoBrojRadnika = null)
    {
        // Stornirani obračun nije isplaćen, pa ne ulazi u godišnju potvrdu o plaćenom porezu.
        var obracuni = _db.ObracuniPlata
            .AsNoTracking()
            .Include(o => o.Radnik)
            .Where(o => o.Godina == godina && !o.Storniran)
            .ToList()
            .Where(o => o.Radnik != null)
            .ToList();

        var nalazi = new List<NalazProvere>();

        if (obracuni.Count == 0)
        {
            nalazi.Add(new NalazProvere
            {
                Tezina = TezinaNalaza.Greska,
                Provera = "Nema obračuna",
                Opis = $"Za {godina}. godinu ne postoji nijedan obračun."
            });
            return new PppPoRezultat { Godina = godina, Nalazi = nalazi };
        }

        // Radnik je periodičan zapis, pa se grupiše po broju radnika, a ne po Id-u.
        var obrasci = new List<PppPoObrazac>();

        foreach (var grupa in obracuni.GroupBy(o => o.Radnik!.BrojRadnika).OrderBy(g => g.Key))
        {
            if (samoBrojRadnika.HasValue && grupa.Key != samoBrojRadnika.Value) continue;

            // Karton iz poslednjeg meseca u godini — na potvrdi stoje aktuelni podaci radnika.
            var poslednji = grupa.OrderByDescending(o => o.Mesec).First();
            var radnik = poslednji.Radnik!;

            var poVrsti = new Dictionary<string, PppPoRed>(StringComparer.Ordinal);

            foreach (var o in grupa.OrderBy(x => x.Mesec))
            {
                string svp = SvpService.Odredi(o);

                if (!poVrsti.TryGetValue(svp, out var red))
                {
                    red = new PppPoRed { Svp = svp };
                    poVrsti[svp] = red;
                }

                red.BrutoPrihod += o.BrutoZarada + o.BrutoBolovanje;
                red.PoreskaOsnovica += o.PoreskaOsnovica;
                red.Porez += o.PorezNaDohodak;
                red.DoprinosPio += o.DoprinosPioRadnik;
                red.DoprinosZdravstvo += o.DoprinosZdravstvoRadnik;
                red.DoprinosNezaposlenost += o.DoprinosNezaposlenostRadnik;
                red.Meseci.Add(o.Mesec);
            }

            var obrazac = new PppPoObrazac
            {
                Godina = godina,
                Radnik = radnik,
                Redovi = poVrsti.Values.OrderBy(r => r.Svp, StringComparer.Ordinal).ToList()
            };

            obrasci.Add(obrazac);
            ProveriObrazac(obrazac, nalazi);
        }

        ProveriSlaganjeSaPrijavama(godina, obracuni, nalazi);

        return new PppPoRezultat { Godina = godina, Obrasci = obrasci, Nalazi = nalazi };
    }

    private static void ProveriObrazac(PppPoObrazac obrazac, List<NalazProvere> nalazi)
    {
        var radnik = obrazac.Radnik;

        NalazProvere Nalaz(TezinaNalaza tezina, string provera, string opis) => new()
        {
            Tezina = tezina,
            BrojRadnika = radnik.BrojRadnika,
            Radnik = radnik.ImeIPrezime,
            Provera = provera,
            Opis = opis
        };

        // Potvrda se uručuje imenom i JMBG-om; bez njega nije upotrebljiva.
        if (string.IsNullOrWhiteSpace(radnik.Jmbg))
            nalazi.Add(Nalaz(TezinaNalaza.Greska, "Nedostaje JMBG", "Potvrda se ne može izdati bez JMBG-a radnika."));

        if (obrazac.UkupnoBruto <= 0)
            nalazi.Add(Nalaz(TezinaNalaza.Upozorenje, "Nema isplata", "Za radnika nije zabeležena nijedna isplata u godini."));
    }

    /// <summary>
    /// Zbir poreza i doprinosa iz obračuna mora da odgovara zbiru iz podnetih PPP-PD prijava.
    /// Razlika znači da je obračun izmenjen posle podnošenja, pa bi potvrda radniku govorila
    /// jedno, a Poreska uprava imala drugo.
    /// </summary>
    private void ProveriSlaganjeSaPrijavama(int godina, List<ObracunPlate> obracuni, List<NalazProvere> nalazi)
    {
        var prijave = _db.PppPdPrijave
            .AsNoTracking()
            .Where(p => p.Godina == godina && p.IznosZaUplatu > 0)
            .ToList();

        if (prijave.Count == 0) return;   // nema sa čim da se poredi

        var meseciSaPrijavom = prijave.Select(p => p.Mesec).Distinct().ToHashSet();

        decimal izObracuna = obracuni
            .Where(o => meseciSaPrijavom.Contains(o.Mesec))
            .Sum(o => o.PorezNaDohodak
                      + o.DoprinosPioRadnik + o.DoprinosZdravstvoRadnik + o.DoprinosNezaposlenostRadnik
                      + o.DoprinosPioPoslodavac + o.DoprinosZdravstvoPoslodavac + o.DoprinosNezaposlenostPoslodavac);

        decimal izPrijava = prijave.Sum(p => p.IznosZaUplatu);

        if (Math.Abs(izObracuna - izPrijava) >= 0.01m)
        {
            nalazi.Add(new NalazProvere
            {
                Tezina = TezinaNalaza.Greska,
                Provera = "Ne slaže se sa PPP-PD prijavama",
                Opis = $"Zbir poreza i doprinosa iz obračuna je {izObracuna:N2}, a iz podnetih prijava {izPrijava:N2} " +
                       $"(razlika {izObracuna - izPrijava:N2}) za mesece {string.Join(", ", meseciSaPrijavom.OrderBy(m => m))}."
            });
        }
    }
}
