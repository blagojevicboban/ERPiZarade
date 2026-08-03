using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using ERPiZaradeData;
using ERPiZaradeData.Models;

namespace ERPiZaradeApp.Services;

/// <summary>Ishod generisanja teksta ugovora.</summary>
public sealed class RezultatTeksta
{
    public bool Uspesno { get; init; }
    public string Tekst { get; init; } = "";

    /// <summary>Polja iz šablona koja nisu popunjena — ostaju vidljiva u tekstu.</summary>
    public IReadOnlyList<string> NepopunjenaPolja { get; init; } = [];

    public string Poruka { get; init; } = "";
}

/// <summary>
/// Generisanje teksta ugovora van radnog odnosa iz šablona (Faza 2.3).
///
/// Zamena je namerno <b>glupa</b>: traži se <c>{Polje}</c> i menja vrednošću. Nema uslova ni
/// petlji u šablonu, jer bi šablon time postao program koji niko ne testira — a piše ga
/// knjigovođa, ne programer.
///
/// Polje koje se ne prepozna ili nije popunjeno <b>ostaje u tekstu</b> i prijavljuje se.
/// Tiho brisanje bi dalo ugovor sa rupom na mestu iznosa ili roka, a to se primeti tek kad
/// je potpisan. Isti obrazac kao kod tolerantnog čitanja ePorezi XML-a: radi se sa onim što
/// postoji i kaže se šta nije prepoznato.
/// </summary>
public class UgovorTekstService
{
    private readonly PlataDbContext _db;

    public UgovorTekstService(PlataDbContext db) => _db = db;

    /// <summary>Polja koja se u šablonu mogu koristiti, sa opisom — prikazuju se uz editor.</summary>
    public static IReadOnlyList<(string Polje, string Opis)> Polja =>
    [
        ("{FirmaNaziv}", "Naziv firme"),
        ("{FirmaAdresa}", "Adresa firme"),
        ("{FirmaGrad}", "Grad firme"),
        ("{FirmaPib}", "PIB firme"),
        ("{FirmaMb}", "Matični broj firme"),
        ("{FirmaZastupnik}", "Ime zastupnika firme"),
        ("{FirmaFunkcijaZastupnika}", "Funkcija zastupnika (direktor…)"),
        ("{PrimalacIme}", "Ime i prezime primaoca"),
        ("{PrimalacJmbg}", "JMBG primaoca"),
        ("{PrimalacAdresa}", "Adresa primaoca"),
        ("{PrimalacMesto}", "Mesto primaoca"),
        ("{PrimalacRacun}", "Tekući račun primaoca"),
        ("{UgovorBroj}", "Broj ugovora"),
        ("{Predmet}", "Predmet ugovora"),
        ("{DatumZakljucenja}", "Datum zaključenja"),
        ("{DatumOd}", "Početak perioda"),
        ("{DatumDo}", "Kraj perioda"),
        ("{Iznos}", "Ugovoreni iznos, brojkama"),
        ("{IznosSlovima}", "Ugovoreni iznos, slovima"),
        ("{VrstaIznosa}", "neto ili bruto — kako je iznos ugovoren"),
        ("{VrstaUgovora}", "Naziv vrste ugovora iz šifarnika"),
        ("{NormiraniTroskovi}", "Normirani troškovi u procentima"),
        ("{StopaPoreza}", "Stopa poreza u procentima"),
        ("{Svp}", "Šifra vrste prihoda"),
        ("{PotpisnikDrugeStrane}", "Naziv druge strane u potpisu (ZA POSLENIKA, ZA AUTORA, ZA IZVRŠIOCA)"),
        ("{Danas}", "Današnji datum")
    ];

    private static readonly Regex PoljeUTekstu = new(@"\{([A-Za-zČĆŠĐŽčćšđž]+)\}", RegexOptions.Compiled);

    /// <summary>
    /// Generiše tekst ugovora iz šablona. Ne upisuje ništa — upis je zasebna odluka, jer
    /// ponovno generisanje briše ručne izmene.
    /// </summary>
    public RezultatTeksta Generisi(int ugovorId, int sablonId)
    {
        var ugovor = _db.Ugovori
            .Include(u => u.VrstaUgovora)
            .FirstOrDefault(u => u.UgovorId == ugovorId);

        if (ugovor == null)
            return new RezultatTeksta { Poruka = "Ugovor nije pronađen." };

        var sablon = _db.SabloniUgovora.FirstOrDefault(s => s.SablonUgovoraId == sablonId);
        if (sablon == null)
            return new RezultatTeksta { Poruka = "Šablon nije pronađen." };

        if (string.IsNullOrWhiteSpace(sablon.Tekst))
            return new RezultatTeksta { Poruka = $"Šablon „{sablon.Naziv}“ je prazan." };

        var firma = _db.Firme.AsNoTracking().FirstOrDefault();

        // Karton primaoca: uzima se poslednji, jer ugovor nije vezan za obračunski period.
        var primalac = _db.Radnici
            .AsNoTracking()
            .Where(r => r.BrojRadnika == ugovor.BrojRadnika)
            .OrderByDescending(r => r.Godina).ThenByDescending(r => r.Mesec)
            .FirstOrDefault();

        var vrednosti = Vrednosti(ugovor, primalac, firma);
        var nepopunjena = new List<string>();

        string tekst = PoljeUTekstu.Replace(sablon.Tekst, m =>
        {
            string polje = m.Value;

            if (!vrednosti.TryGetValue(polje, out string? vrednost) || string.IsNullOrWhiteSpace(vrednost))
            {
                if (!nepopunjena.Contains(polje, StringComparer.Ordinal)) nepopunjena.Add(polje);
                return polje;   // ostaje vidljivo, da se rupa primeti pre potpisa
            }

            return vrednost;
        });

        return new RezultatTeksta
        {
            Uspesno = true,
            Tekst = tekst,
            NepopunjenaPolja = nepopunjena,
            Poruka = nepopunjena.Count == 0
                ? $"Tekst je generisan iz šablona „{sablon.Naziv}“."
                : $"Tekst je generisan; nepopunjeno je {nepopunjena.Count} polja: {string.Join(", ", nepopunjena)}."
        };
    }

    /// <summary>Šablon koji se podrazumeva za vrstu ugovora; opšti kad posebnog nema.</summary>
    public SablonUgovora? PodrazumevaniSablon(Ugovor ugovor)
        => _db.SabloniUgovora
               .Where(s => s.Aktivan && s.VrstaUgovoraId == ugovor.VrstaUgovoraId)
               .OrderBy(s => s.Redosled)
               .FirstOrDefault()
           ?? _db.SabloniUgovora
               .Where(s => s.Aktivan && s.VrstaUgovoraId == null)
               .OrderBy(s => s.Redosled)
               .FirstOrDefault();

    public RezultatTeksta Sacuvaj(int ugovorId, string tekst)
    {
        var ugovor = _db.Ugovori.FirstOrDefault(u => u.UgovorId == ugovorId);
        if (ugovor == null)
            return new RezultatTeksta { Poruka = "Ugovor nije pronađen." };

        ugovor.Tekst = tekst ?? "";
        ugovor.DatumTeksta = DateTime.Now;
        _db.SaveChanges();

        return new RezultatTeksta { Uspesno = true, Tekst = ugovor.Tekst, Poruka = "Tekst ugovora je sačuvan." };
    }

    private static Dictionary<string, string> Vrednosti(Ugovor ugovor, Radnik? primalac, Firma? firma)
    {
        var vrsta = ugovor.VrstaUgovora;

        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["{FirmaNaziv}"] = firma?.Naziv ?? "",
            ["{FirmaAdresa}"] = firma?.Adresa ?? "",
            ["{FirmaGrad}"] = firma?.Grad ?? "",
            ["{FirmaPib}"] = firma?.Pib ?? "",
            ["{FirmaMb}"] = firma?.Mb ?? "",
            ["{FirmaZastupnik}"] = firma?.Zastupnik ?? "",
            ["{FirmaFunkcijaZastupnika}"] = firma?.FunkcijaZastupnika ?? "",

            ["{PrimalacIme}"] = primalac?.ImeIPrezime ?? "",
            ["{PrimalacJmbg}"] = primalac?.Jmbg ?? "",
            ["{PrimalacAdresa}"] = primalac?.AdresaStanovanja ?? "",
            ["{PrimalacMesto}"] = primalac?.Mesto ?? "",
            ["{PrimalacRacun}"] = primalac?.BankovniRacun ?? "",

            ["{UgovorBroj}"] = ugovor.Broj,
            ["{Predmet}"] = ugovor.Predmet,
            ["{DatumZakljucenja}"] = ugovor.DatumZakljucenja.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture),
            ["{DatumOd}"] = ugovor.DatumOd?.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture) ?? "",
            ["{DatumDo}"] = ugovor.DatumDo?.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture) ?? "",

            ["{Iznos}"] = ugovor.UgovorenIznos.ToString("N2", new CultureInfo("sr-Latn-RS")),
            ["{IznosSlovima}"] = IznosSlovima(ugovor.UgovorenIznos),
            ["{VrstaIznosa}"] = ugovor.IznosJeNeto ? "neto" : "bruto",

            ["{VrstaUgovora}"] = vrsta?.Naziv ?? "",
            ["{NormiraniTroskovi}"] = (vrsta?.NormiraniTroskoviProcenat ?? 0m).ToString("0.##", CultureInfo.InvariantCulture),
            ["{StopaPoreza}"] = (vrsta?.StopaPoreza ?? 0m).ToString("0.##", CultureInfo.InvariantCulture),
            ["{Svp}"] = SvpService.Sastavi(ugovor.TipPrimaoca, vrsta?.Ovp),

            ["{PotpisnikDrugeStrane}"] = PotpisnikDrugeStrane(vrsta?.Ovp),
            ["{Danas}"] = DateTime.Today.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)
        };
    }

    /// <summary>
    /// Kako se druga strana zove u potpisu. Naziv se razlikuje po vrsti posla — poslenik,
    /// autor, izvršilac — a izvodi se iz oznake vrste prihoda, jer je ona ta koja kaže o kom
    /// se prihodu radi.
    /// </summary>
    private static string PotpisnikDrugeStrane(string? ovp) => ovp switch
    {
        "301" or "302" or "303" => "ZA AUTORA",
        "150" or "151" or "152" => "ZA IZVRŠIOCA",
        _ => "ZA POSLENIKA"
    };

    // ── Iznos slovima ────────────────────────────────────────────────

    private static readonly string[] Jedinice =
        ["", "jedan", "dva", "tri", "četiri", "pet", "šest", "sedam", "osam", "devet"];

    /// <summary>„Hiljada" i „milijarda" su ženskog roda, pa uz njih ide „jedna" i „dve".</summary>
    private static readonly string[] JediniceZenski =
        ["", "jedna", "dve", "tri", "četiri", "pet", "šest", "sedam", "osam", "devet"];

    private static readonly string[] OdDesetDoDevetnaest =
        ["deset", "jedanaest", "dvanaest", "trinaest", "četrnaest", "petnaest",
         "šesnaest", "sedamnaest", "osamnaest", "devetnaest"];

    private static readonly string[] Desetice =
        ["", "", "dvadeset", "trideset", "četrdeset", "pedeset",
         "šezdeset", "sedamdeset", "osamdeset", "devedeset"];

    private static readonly string[] Stotine =
        ["", "sto", "dvesta", "trista", "četiristo", "petsto",
         "šeststo", "sedamsto", "osamsto", "devetsto"];

    /// <summary>
    /// Iznos ispisan slovima, kako se u ugovoru piše uz brojku. Postoji zato što se razlika
    /// brojke i slova tumači u korist slova — pa ne sme da bude prepisana rukom.
    /// </summary>
    public static string IznosSlovima(decimal iznos)
    {
        if (iznos < 0) return "minus " + IznosSlovima(-iznos);

        long dinara = (long)decimal.Truncate(iznos);
        int para = (int)Math.Round((iznos - dinara) * 100m, MidpointRounding.AwayFromZero);

        // Zaokrugljivanje para naviše sme da prelije u dinar (npr. 9,999).
        if (para == 100) { dinara++; para = 0; }

        var sb = new StringBuilder();
        sb.Append(dinara == 0 ? "nula" : Grupe(dinara));
        sb.Append(' ').Append(Oblik(dinara, "dinar", "dinara", "dinara"));

        if (para > 0)
        {
            sb.Append(" i ").Append(DoHiljadu(para, zenskiRod: true));
            sb.Append(' ').Append(Oblik(para, "para", "pare", "para"));
        }

        return sb.ToString();
    }

    /// <summary>
    /// Oblik imenice uz broj, po pravilu srpskog jezika: jednina uz brojeve koji se završavaju
    /// na 1 (osim 11), množina „dve/tri/četiri" uz 2–4 (osim 12–14), a inače množina uz pet i
    /// više. Bez ovoga se dobija „dva hiljada" i „dvadesetjedan dinara".
    /// </summary>
    private static string Oblik(long broj, string jednina, string dvaTriCetiri, string pet)
    {
        long poslednjeDve = broj % 100;
        long poslednja = broj % 10;

        if (poslednjeDve is >= 11 and <= 14) return pet;
        if (poslednja == 1) return jednina;
        if (poslednja is >= 2 and <= 4) return dvaTriCetiri;
        return pet;
    }

    private static string Grupe(long broj)
    {
        if (broj == 0) return "";

        var sb = new StringBuilder();

        long milijarde = broj / 1_000_000_000;
        long milioni = broj / 1_000_000 % 1000;
        long hiljade = broj / 1000 % 1000;
        int ostatak = (int)(broj % 1000);

        if (milijarde > 0)
        {
            sb.Append(DoHiljadu((int)milijarde, zenskiRod: true))
              .Append(Oblik(milijarde, "milijarda", "milijarde", "milijardi"));
        }

        if (milioni > 0)
        {
            // „milion", a ne „jedanmilion" — tako se govori i tako se piše u ugovorima.
            sb.Append(milioni == 1 ? "milion" : DoHiljadu((int)milioni) + Oblik(milioni, "milion", "miliona", "miliona"));
        }

        if (hiljade > 0)
        {
            // Isto pravilo: „hiljadu", a ne „jednahiljada".
            sb.Append(hiljade == 1
                ? "hiljadu"
                : DoHiljadu((int)hiljade, zenskiRod: true) + Oblik(hiljade, "hiljada", "hiljade", "hiljada"));
        }

        if (ostatak > 0) sb.Append(DoHiljadu(ostatak));

        return sb.ToString();
    }

    private static string DoHiljadu(int broj, bool zenskiRod = false)
    {
        if (broj == 0) return "";

        var sb = new StringBuilder();
        sb.Append(Stotine[broj / 100]);

        int ostatak = broj % 100;

        if (ostatak >= 10 && ostatak <= 19)
        {
            sb.Append(OdDesetDoDevetnaest[ostatak - 10]);
        }
        else
        {
            sb.Append(Desetice[ostatak / 10]);
            sb.Append((zenskiRod ? JediniceZenski : Jedinice)[ostatak % 10]);
        }

        return sb.ToString();
    }
}
