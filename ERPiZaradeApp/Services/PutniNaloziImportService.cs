using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ERPiZaradeData;
using ERPiZaradeData.Models;

namespace ERPiZaradeApp.Services;

// ── Oblik fajla koji ERPiFinansije zapisuje ──────────────────────────
// Nazivi svojstava su nazivi polja u fajlu i moraju odgovarati zapisivaču na drugoj strani
// (PutniNaloziZaZaradeWriter u ERPiFinansije). Menjaju se samo uz podizanje Verzija.

internal sealed class PutniNaloziZaZaradeFajl
{
    public string? Format { get; set; }
    public int Verzija { get; set; }
    public string? Izvor { get; set; }
    public PnzFirma? Firma { get; set; }
    public int Godina { get; set; }
    public int Mesec { get; set; }
    public List<PnzStavka>? Stavke { get; set; }
}

internal sealed class PnzFirma
{
    public string? Naziv { get; set; }
    public string? Pib { get; set; }
    public string? MaticniBroj { get; set; }
}

internal sealed class PnzStavka
{
    public string? Jmbg { get; set; }
    public string? ZaposleniIme { get; set; }
    public string? BrojNaloga { get; set; }
    public string? DatumPovratka { get; set; }
    public decimal UkupnoDnevnice { get; set; }
    public decimal NeoporeziviDeo { get; set; }
    public decimal PrekoracenjeDnevnice { get; set; }
}

/// <summary>Jedna stavka fajla, spremna za prikaz pre potvrde uvoza.</summary>
public sealed class StavkaZaUvoz
{
    public required string Jmbg { get; init; }
    public required string ZaposleniIme { get; init; }
    public required string BrojNaloga { get; init; }
    public required DateTime DatumPovratka { get; init; }
    public required decimal Iznos { get; init; }

    /// <summary>Nađeni radnik u ciljnom periodu; <c>null</c> ako JMBG nije uparen.</summary>
    public Radnik? UparenRadnik { get; init; }

    public bool VecUvezen { get; init; }

    /// <summary>Kad je postavljeno, ova stavka se ne uvozi — razlog se prikazuje uz nju.</summary>
    public string? Greska { get; init; }

    public bool SmeSeUvesti => Greska == null;
}

/// <summary>Šta je pročitano iz fajla i šta bi uvoz izostavio.</summary>
public sealed class RezultatUvozaPutnihNaloga
{
    public IReadOnlyList<StavkaZaUvoz> Stavke { get; init; } = [];
    public IReadOnlyList<NalazProvere> Nalazi { get; init; } = [];

    public int Godina { get; init; }
    public int Mesec { get; init; }
    public string FirmaNaziv { get; init; } = "";
    public string? FirmaPib { get; init; }

    /// <summary>
    /// Konačna zarada perioda kojoj stavke pripadaju. <c>null</c> znači da period još nema
    /// nijednu isplatu — <see cref="PutniNaloziImportService.UveziAsync"/> je pravi sam, isto
    /// kao svaki drugi ekran koji za isplate ne zna.
    /// </summary>
    public Isplata? CiljnaIsplata { get; init; }

    public int BrojGresaka => Nalazi.Count(n => n.Tezina == TezinaNalaza.Greska)
                             + Stavke.Count(s => !s.SmeSeUvesti);

    public int BrojZaUvoz => Stavke.Count(s => s.SmeSeUvesti);

    public bool SmeSeUvesti => Nalazi.All(n => n.Tezina != TezinaNalaza.Greska) && BrojZaUvoz > 0;
}

/// <summary>
/// Uvoz prekoračenja neoporezive dnevnice iz ERPiFinansije (Faza 3.2).
///
/// Fajl već nosi izračunat iznos — ovde se ništa ne računa, samo se upari radnik po JMBG-u i
/// upisuje kao <see cref="UnetoPrimanje"/> vrste <see cref="VrstePrimanjaSeed.DnevnicaPrekoracenje"/>,
/// vezano za konačnu zaradu ciljnog meseca. Isti princip kao <c>ZaradeImportService</c> na
/// drugoj strani: prepisivanje, ne računanje.
/// </summary>
public class PutniNaloziImportService
{
    /// <summary>Oznaka po kojoj se fajl prepoznaje.</summary>
    public const string OznakaFormata = "ERPi-putni-nalozi-za-zarade";

    /// <summary>Najviša verzija formata koju ovaj program ume da pročita.</summary>
    public const int PodrzanaVerzija = 1;

    private static readonly JsonSerializerOptions Opcije = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    private readonly PlataDbContext _db;

    public PutniNaloziImportService(PlataDbContext db) => _db = db;

    /// <summary>
    /// Čita fajl i priprema stavke za uvoz, ali ništa ne snima — korisnik prvo vidi šta je
    /// pročitano i koji radnik je uparen, pa tek onda potvrđuje.
    /// </summary>
    public async Task<RezultatUvozaPutnihNaloga> ProcitajAsync(string putanja)
    {
        var nalazi = new List<NalazProvere>();

        PutniNaloziZaZaradeFajl? fajl;
        try
        {
            var tekst = await System.IO.File.ReadAllTextAsync(putanja);
            fajl = JsonSerializer.Deserialize<PutniNaloziZaZaradeFajl>(tekst, Opcije);
        }
        catch (Exception ex)
        {
            nalazi.Add(new NalazProvere
            {
                Tezina = TezinaNalaza.Greska,
                Provera = "Fajl se ne može pročitati",
                Opis = ex.Message
            });
            return new RezultatUvozaPutnihNaloga { Nalazi = nalazi };
        }

        if (fajl == null || !string.Equals(fajl.Format, OznakaFormata, StringComparison.Ordinal))
        {
            nalazi.Add(new NalazProvere
            {
                Tezina = TezinaNalaza.Greska,
                Provera = "Nije izvoz iz ERPiFinansije",
                Opis = $"Fajl ne nosi oznaku „{OznakaFormata}“. Izvezite ga iz ERPiFinansije, " +
                       "„Putni nalozi“ → „📤 Izvoz za zarade“."
            });
            return new RezultatUvozaPutnihNaloga { Nalazi = nalazi };
        }

        if (fajl.Verzija > PodrzanaVerzija)
        {
            nalazi.Add(new NalazProvere
            {
                Tezina = TezinaNalaza.Greska,
                Provera = "Novija verzija formata",
                Opis = $"Fajl je verzije {fajl.Verzija}, a ovaj program čita do {PodrzanaVerzija}. " +
                       "Nadogradite ERPiZarade."
            });
            return new RezultatUvozaPutnihNaloga { Nalazi = nalazi };
        }

        var stavkeFajla = fajl.Stavke ?? [];
        if (stavkeFajla.Count == 0)
        {
            nalazi.Add(new NalazProvere
            {
                Tezina = TezinaNalaza.Greska,
                Provera = "Fajl je prazan",
                Opis = "U fajlu nema nijedne stavke."
            });
            return new RezultatUvozaPutnihNaloga { Nalazi = nalazi };
        }

        // Firma se proverava kao dodatna potvrda, ne kao ključ uparivanja — PIB je u
        // ERPiFinansije opciono polje i ne mora biti popunjen na obe strane.
        var firma = _db.Firme.AsNoTracking().FirstOrDefault();
        if (firma != null && !string.IsNullOrWhiteSpace(fajl.Firma?.Pib)
            && !string.IsNullOrWhiteSpace(firma.Pib)
            && !string.Equals(firma.Pib.Trim(), fajl.Firma.Pib.Trim(), StringComparison.Ordinal))
        {
            nalazi.Add(new NalazProvere
            {
                Tezina = TezinaNalaza.Upozorenje,
                Provera = "PIB firme se ne poklapa",
                Opis = $"Fajl je izvezen za PIB {fajl.Firma.Pib}, a ova baza vodi PIB {firma.Pib}. " +
                       "Proverite da li je ovo pravi fajl pre nego što potvrdite uvoz."
            });
        }

        // Ciljna isplata: uvek konačna zarada meseca, nikad akontacija (odeljak 5.5 plana).
        var isplataService = new IsplataService(_db);
        var isplateZarade = isplataService.Isplate(fajl.Godina, fajl.Mesec, RodIsplate.Zarada);
        var konacna = isplateZarade.FirstOrDefault(i => i.Vrsta == VrstaIsplate.KonacnaZarada);

        if (isplateZarade.Count > 0 && konacna == null)
        {
            nalazi.Add(new NalazProvere
            {
                Tezina = TezinaNalaza.Greska,
                Provera = "Mesec nema konačnu zaradu",
                Opis = $"{fajl.Mesec:D2}/{fajl.Godina} već ima isplatu, ali nijedna nije konačna " +
                       "zarada. Prekoračenje dnevnice ide na konačnu zaradu, ne na akontaciju — " +
                       "napravite je pa uvezite ponovo."
            });
        }

        var vrstaId = _db.VrstePrimanja
            .Where(v => v.Sifra == VrstePrimanjaSeed.DnevnicaPrekoracenje)
            .Select(v => v.VrstaPrimanjaId)
            .FirstOrDefault();

        var stavke = new List<StavkaZaUvoz>();

        foreach (var s in stavkeFajla)
        {
            string jmbg = (s.Jmbg ?? "").Trim();
            string zaposleni = (s.ZaposleniIme ?? "").Trim();
            string brojNaloga = (s.BrojNaloga ?? "").Trim();
            DateTime.TryParse(s.DatumPovratka, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var datumPovratka);

            string? greska = null;
            Radnik? radnik = null;

            if (string.IsNullOrWhiteSpace(jmbg))
            {
                greska = "Nalog nema JMBG — proverite unos na putnom nalogu u ERPiFinansije.";
            }
            else
            {
                var kandidati = _db.Radnici
                    .Where(r => r.Jmbg == jmbg && r.Godina == fajl.Godina && r.Mesec == fajl.Mesec)
                    .ToList();

                if (kandidati.Count == 0)
                {
                    greska = $"Radnik sa JMBG {jmbg} nije nađen u {fajl.Mesec:D2}/{fajl.Godina} — " +
                              "proverite karton radnika ili unos u putnom nalogu.";
                }
                else if (kandidati.Count > 1)
                {
                    greska = $"Više radnika u ovom mesecu ima JMBG {jmbg} — ne pogađa se koji je pravi.";
                }
                else
                {
                    radnik = kandidati[0];
                }
            }

            bool vecUvezen = false;
            if (radnik != null && vrstaId != 0 && !string.IsNullOrWhiteSpace(brojNaloga))
            {
                vecUvezen = _db.UnetaPrimanja.Any(p =>
                    p.RadnikId == radnik.Id && p.VrstaPrimanjaId == vrstaId
                    && p.Godina == fajl.Godina && p.Mesec == fajl.Mesec
                    && p.Napomena.Contains(brojNaloga));

                if (vecUvezen && greska == null)
                {
                    greska = $"Nalog {brojNaloga} je već uvezen za ovog radnika u ovom mesecu.";
                }
            }

            if (greska == null && isplateZarade.Count > 0 && konacna == null)
            {
                greska = "Mesec nema konačnu zaradu (videti nalaz iznad).";
            }

            stavke.Add(new StavkaZaUvoz
            {
                Jmbg = jmbg,
                ZaposleniIme = zaposleni,
                BrojNaloga = brojNaloga,
                DatumPovratka = datumPovratka,
                Iznos = s.PrekoracenjeDnevnice,
                UparenRadnik = radnik,
                VecUvezen = vecUvezen,
                Greska = greska
            });
        }

        if (vrstaId == 0)
        {
            nalazi.Add(new NalazProvere
            {
                Tezina = TezinaNalaza.Greska,
                Provera = "Vrsta primanja nije u šifarniku",
                Opis = $"Šifra „{VrstePrimanjaSeed.DnevnicaPrekoracenje}“ ne postoji u „Vrste primanja“ — " +
                       "pokrenite program bar jednom da se šifarnik dopuni, pa uvezite ponovo."
            });
        }

        return new RezultatUvozaPutnihNaloga
        {
            Stavke = stavke,
            Nalazi = nalazi,
            Godina = fajl.Godina,
            Mesec = fajl.Mesec,
            FirmaNaziv = fajl.Firma?.Naziv ?? "",
            FirmaPib = fajl.Firma?.Pib,
            CiljnaIsplata = konacna
        };
    }

    /// <summary>
    /// Upisuje stavke koje su prošle provere. Konačna zarada se pravi sama ako period još
    /// nema nijednu isplatu — isto pravilo kao <see cref="IsplataService.Obezbedi"/> svuda
    /// drugde; ako mesec ima isplate a nijedna nije konačna, <see cref="ProcitajAsync"/> je to
    /// već pretvorio u grešku i ovamo se ne stiže.
    ///
    /// Dva putna naloga istog radnika u istom mesecu se <b>spajaju</b> u jedan red — isto
    /// pravilo koje ekran „Primanja“ već traži za svaku vrstu primanja („jedan iznos po
    /// radniku, periodu i vrsti“), i ono što jedinstveni indeks na <c>UnetaPrimanja</c>
    /// zahteva. Ponovni uvoz istog naloga je uhvaćen ranije, u <see cref="ProcitajAsync"/>
    /// (provera „već uvezen“), pa se ovde ne proverava ponovo.
    /// </summary>
    public int Uvezi(RezultatUvozaPutnihNaloga rezultat)
    {
        if (!rezultat.SmeSeUvesti) return 0;

        var isplata = rezultat.CiljnaIsplata
            ?? new IsplataService(_db).Obezbedi(rezultat.Godina, rezultat.Mesec);

        int vrstaId = _db.VrstePrimanja
            .Single(v => v.Sifra == VrstePrimanjaSeed.DnevnicaPrekoracenje)
            .VrstaPrimanjaId;

        var grupe = rezultat.Stavke
            .Where(s => s.SmeSeUvesti && s.UparenRadnik != null)
            .GroupBy(s => s.UparenRadnik!.Id);

        int uvezeno = 0;
        foreach (var grupa in grupe)
        {
            int radnikId = grupa.Key;
            decimal iznos = grupa.Sum(s => s.Iznos);
            string napisNaloga = string.Join(", ", grupa.Select(s =>
                $"{s.BrojNaloga} ({s.DatumPovratka:dd.MM.yyyy})"));

            var postojece = _db.UnetaPrimanja.FirstOrDefault(p =>
                p.RadnikId == radnikId && p.VrstaPrimanjaId == vrstaId
                && p.Godina == rezultat.Godina && p.Mesec == rezultat.Mesec
                && p.IsplataId == isplata.IsplataId);

            if (postojece != null)
            {
                postojece.Iznos += iznos;
                postojece.Napomena = string.IsNullOrWhiteSpace(postojece.Napomena)
                    ? $"Putni nalozi: {napisNaloga}"
                    : $"{postojece.Napomena}; {napisNaloga}";
            }
            else
            {
                _db.UnetaPrimanja.Add(new UnetoPrimanje
                {
                    RadnikId = radnikId,
                    Godina = rezultat.Godina,
                    Mesec = rezultat.Mesec,
                    VrstaPrimanjaId = vrstaId,
                    IsplataId = isplata.IsplataId,
                    Iznos = iznos,
                    Napomena = $"Putni nalozi: {napisNaloga}"
                });
            }

            uvezeno += grupa.Count();
        }

        if (uvezeno > 0) _db.SaveChanges();
        return uvezeno;
    }
}
