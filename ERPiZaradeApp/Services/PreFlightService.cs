using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using ERPiZaradeData;
using ERPiZaradeData.Models;

namespace ERPiZaradeApp.Services;

/// <summary>Težina nalaza pre-flight provere.</summary>
public enum TezinaNalaza
{
    /// <summary>Obračun je upotrebljiv, ali nešto nedostaje za kasnije korake (npr. e-mail).</summary>
    Upozorenje = 0,

    /// <summary>Prijava ili isplata bi pala — zaključavanje se ne dozvoljava bez potvrde administratora.</summary>
    Greska = 1
}

/// <summary>Jedan nalaz kontrolne provere.</summary>
public sealed class NalazProvere
{
    public TezinaNalaza Tezina { get; init; }
    public int? BrojRadnika { get; init; }
    public string Radnik { get; init; } = "";
    public string Provera { get; init; } = "";
    public string Opis { get; init; } = "";

    public string TezinaTekst => Tezina == TezinaNalaza.Greska ? "Greška" : "Upozorenje";
}

/// <summary>Zbirni rezultat pre-flight provere jednog obračunskog perioda.</summary>
public sealed class RezultatProvere
{
    public int Godina { get; init; }
    public int Mesec { get; init; }
    public int BrojObracuna { get; init; }
    public IReadOnlyList<NalazProvere> Nalazi { get; init; } = [];

    public int BrojGresaka => Nalazi.Count(n => n.Tezina == TezinaNalaza.Greska);
    public int BrojUpozorenja => Nalazi.Count(n => n.Tezina == TezinaNalaza.Upozorenje);
    public bool JeCist => Nalazi.Count == 0;

    /// <summary>Da li period sme da se zaključa bez izričite potvrde administratora.</summary>
    public bool SmeSeZakljucati => BrojGresaka == 0;
}

/// <summary>
/// Kontrolne provere koje se izvršavaju PRE zaključavanja obračuna, PPP-PD prijave i naloga
/// za prenos. Ispravka posle podnošenja prijave košta izmenjenu prijavu i storniranje, pa je
/// smisao ovog servisa da se svi poznati problemi vide na jednom mestu dok su još jeftini.
///
/// Provere su namerno samo za čitanje — ništa ne menjaju i ne popravljaju.
/// </summary>
public class PreFlightService
{
    private readonly PlataDbContext _db;

    public PreFlightService(PlataDbContext db) => _db = db;

    public RezultatProvere Proveri(int godina, int mesec)
    {
        var obracuni = _db.ObracuniPlata
            .AsNoTracking()
            .Include(o => o.Radnik)
            .Where(o => o.Godina == godina && o.Mesec == mesec)
            .ToList();

        var nalazi = new List<NalazProvere>();

        if (obracuni.Count == 0)
        {
            nalazi.Add(new NalazProvere
            {
                Tezina = TezinaNalaza.Greska,
                Provera = "Prazan period",
                Opis = $"Za period {mesec:D2}/{godina} ne postoji nijedan obračun."
            });

            return new RezultatProvere { Godina = godina, Mesec = mesec, BrojObracuna = 0, Nalazi = nalazi };
        }

        decimal najnizaOsnovica = _db.Doprinosi
            .AsNoTracking()
            .Where(d => d.Godina == godina && d.Mesec == mesec)
            .Select(d => d.NajnizaOsnovica)
            .FirstOrDefault();

        foreach (var o in obracuni)
        {
            nalazi.AddRange(ProveriObracun(o, najnizaOsnovica));
        }

        ProveriDuplikate(obracuni, nalazi);

        return new RezultatProvere
        {
            Godina = godina,
            Mesec = mesec,
            BrojObracuna = obracuni.Count,
            Nalazi = nalazi
        };
    }

    private static IEnumerable<NalazProvere> ProveriObracun(ObracunPlate o, decimal najnizaOsnovica)
    {
        var radnik = o.Radnik;
        string ime = radnik?.ImeIPrezime ?? $"(radnik #{o.RadnikId})";
        int? broj = radnik?.BrojRadnika;

        NalazProvere Nalaz(TezinaNalaza tezina, string provera, string opis) => new()
        {
            Tezina = tezina,
            BrojRadnika = broj,
            Radnik = ime,
            Provera = provera,
            Opis = opis
        };

        // 1. Negativan neto — po pravilu previsoke obustave ili pogrešan unos sati.
        if (o.NetoIsplata < 0)
        {
            yield return Nalaz(TezinaNalaza.Greska, "Negativan neto",
                $"Neto za isplatu je {o.NetoIsplata:N2}. Obustave verovatno premašuju zaradu.");
        }

        // 2. Bruto ispod najniže osnovice doprinosa — PPP-PD bi bio odbijen.
        decimal bruto = o.BrutoZarada + o.BrutoBolovanje;
        if (najnizaOsnovica > 0 && bruto > 0 && bruto < najnizaOsnovica)
        {
            yield return Nalaz(TezinaNalaza.Greska, "Bruto ispod najniže osnovice",
                $"Bruto {bruto:N2} je ispod najniže osnovice doprinosa {najnizaOsnovica:N2}.");
        }

        if (radnik == null)
        {
            yield return Nalaz(TezinaNalaza.Greska, "Radnik ne postoji",
                "Obračun nije vezan ni za jedan karton radnika.");
            yield break;
        }

        // 3. JMBG — bez njega radnik ispada iz PPP-PD prijave, i to nečujno.
        if (string.IsNullOrWhiteSpace(radnik.Jmbg))
        {
            yield return Nalaz(TezinaNalaza.Greska, "Nedostaje JMBG",
                "Radnik bez JMBG-a se izostavlja iz PPP-PD prijave.");
        }
        else if (!JmbgValidator.Validate(radnik.Jmbg, out string jmbgGreska))
        {
            yield return Nalaz(TezinaNalaza.Greska, "Neispravan JMBG", jmbgGreska);
        }

        // 4. Tekući račun — bez njega nema naloga za prenos ni spiska za isplatu.
        if (string.IsNullOrWhiteSpace(radnik.BankovniRacun))
        {
            yield return Nalaz(TezinaNalaza.Greska, "Nedostaje tekući račun",
                "Radnik nema tekući račun, pa se ne može uvrstiti u nalog za prenos.");
        }

        // 5. E-mail — smeta samo slanju listića, obračun je i bez njega ispravan.
        if (string.IsNullOrWhiteSpace(radnik.Email))
        {
            yield return Nalaz(TezinaNalaza.Upozorenje, "Nedostaje e-mail",
                "Radnik nema e-mail adresu, pa mu se platni listić ne može poslati.");
        }

        // 6. Sati veći od mesečnog fonda — prekovremeni se vode odvojeno, pa ovo znači grešku u unosu.
        int fond = (int)Math.Round(o.FondSatiMesecni);
        int satiBezPrekovremenih = o.UkupnoSati - o.PrekovremeneSati;
        if (fond > 0 && satiBezPrekovremenih > fond)
        {
            yield return Nalaz(TezinaNalaza.Greska, "Sati veći od fonda",
                $"Uneto {satiBezPrekovremenih} sati (bez prekovremenih) uz mesečni fond od {fond}.");
        }

        // 7. Istekla poreska olakšica koja se i dalje primenjuje.
        bool primenjujeOlaksicu = radnik.ProcenatPovracajaPoreza > 0 || radnik.ProcenatPovracajaDoprinosa > 0;
        if (primenjujeOlaksicu && radnik.OlaksicaVaziDo.HasValue)
        {
            var krajPerioda = new DateTime(o.Godina, o.Mesec, DateTime.DaysInMonth(o.Godina, o.Mesec));
            if (radnik.OlaksicaVaziDo.Value < krajPerioda)
            {
                yield return Nalaz(TezinaNalaza.Greska, "Istekla poreska olakšica",
                    $"Olakšica je važila do {radnik.OlaksicaVaziDo.Value:dd.MM.yyyy}, a i dalje se primenjuje.");
            }
        }
    }

    /// <summary>
    /// Dva obračuna za isti JMBG u istom periodu daju dva reda u PPP-PD prijavi za isto lice —
    /// Poreska uprava to odbija.
    /// </summary>
    private static void ProveriDuplikate(List<ObracunPlate> obracuni, List<NalazProvere> nalazi)
    {
        var duplikati = obracuni
            .Where(o => o.Radnik != null && !string.IsNullOrWhiteSpace(o.Radnik.Jmbg))
            .GroupBy(o => o.Radnik!.Jmbg)
            .Where(g => g.Count() > 1);

        foreach (var grupa in duplikati)
        {
            var prvi = grupa.First();
            nalazi.Add(new NalazProvere
            {
                Tezina = TezinaNalaza.Greska,
                BrojRadnika = prvi.Radnik!.BrojRadnika,
                Radnik = prvi.Radnik.ImeIPrezime,
                Provera = "Dupli obračun",
                Opis = $"Za JMBG {grupa.Key} postoji {grupa.Count()} obračuna u istom periodu."
            });
        }
    }
}
