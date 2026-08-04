using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using ERPiZaradeData;
using ERPiZaradeData.Models;

namespace ERPiZaradeApp.Services;

/// <summary>
/// Razložen obračun jedne naknade van radnog odnosa. Odvojen je od <see cref="ObracunPlate"/>
/// da bi se računica mogla proveriti i prikazati pre nego što išta uđe u bazu — ista svrha
/// koju ima proba u <see cref="PrevodStavkiService"/>.
/// </summary>
public sealed class ObracunUgovora
{
    public decimal Bruto { get; init; }
    public decimal NormiraniTroskovi { get; init; }

    /// <summary>Osnovica poreza i doprinosa — bruto umanjen za normirane troškove.</summary>
    public decimal Osnovica { get; init; }

    public decimal Porez { get; init; }

    public decimal PioPrimalac { get; init; }
    public decimal ZdravstvoPrimalac { get; init; }
    public decimal NezaposlenostPrimalac { get; init; }

    public decimal PioIsplatilac { get; init; }
    public decimal ZdravstvoIsplatilac { get; init; }
    public decimal NezaposlenostIsplatilac { get; init; }

    public decimal DoprinosiPrimaoca => PioPrimalac + ZdravstvoPrimalac + NezaposlenostPrimalac;
    public decimal DoprinosiIsplatioca => PioIsplatilac + ZdravstvoIsplatilac + NezaposlenostIsplatilac;

    /// <summary>Ono što primalac dobija na račun.</summary>
    public decimal Neto => Bruto - Porez - DoprinosiPrimaoca;

    /// <summary>Ukupan trošak isplatioca — bruto plus doprinosi koje on snosi.</summary>
    public decimal UkupanTrosak => Bruto + DoprinosiIsplatioca;

    /// <summary>Zbir koji ide jednom uplatom na objedinjeni račun, uz BOP iz prijave.</summary>
    public decimal PoreziIDoprinosi => Porez + DoprinosiPrimaoca + DoprinosiIsplatioca;
}

/// <summary>Ishod radnje nad obračunom po ugovoru.</summary>
public sealed class RezultatUgovora
{
    public bool Uspesno { get; init; }
    public ObracunPlate? Obracun { get; init; }
    public ObracunUgovora? Racunica { get; init; }
    public string Poruka { get; init; } = "";
}

/// <summary>
/// Obračun naknada van radnog odnosa (Faza 2.3) — ugovor o delu, autorska naknada,
/// privremeni i povremeni poslovi, naknada članovima organa upravljanja.
///
/// Računica je za sve njih ista i ima samo četiri koraka; razlikuju se <b>brojevi</b>, a oni
/// stoje u šifarniku <see cref="VrstaUgovora"/>:
/// <list type="number">
///   <item>osnovica = bruto − normirani troškovi,</item>
///   <item>porez = osnovica × stopa poreza,</item>
///   <item>doprinosi = osnovica × stope, podeljeni na teret primaoca i isplatioca,</item>
///   <item>neto = bruto − porez − doprinosi na teret primaoca.</item>
/// </list>
///
/// Rezultat se upisuje u <see cref="ObracunPlate"/> — isti zapis kao zarada. Zbog toga
/// PPP-PD prijava, nalozi za prenos i godišnja potvrda rade nad naknadama <b>bez ijedne
/// izmene</b>: sve što ih razlikuje od zarade je šifra vrste prihoda i to što se ne mere
/// satima. Obračun se vezuje za <see cref="Isplata"/>, jer se naknada isplaćuje kad se
/// isplaćuje, a ne krajem meseca.
/// </summary>
public class UgovorObracunService
{
    private readonly PlataDbContext _db;

    public UgovorObracunService(PlataDbContext db) => _db = db;

    // ── Računica ─────────────────────────────────────────────────────

    /// <summary>Razlaže bruto naknadu na porez, doprinose i neto.</summary>
    public static ObracunUgovora Izracunaj(VrstaUgovora vrsta, decimal bruto)
    {
        ArgumentNullException.ThrowIfNull(vrsta);

        bruto = Math.Round(bruto, 2, MidpointRounding.AwayFromZero);

        decimal normirani = Procenat(bruto, vrsta.NormiraniTroskoviProcenat);
        decimal osnovica = bruto - normirani;

        return new ObracunUgovora
        {
            Bruto = bruto,
            NormiraniTroskovi = normirani,
            Osnovica = osnovica,
            Porez = Procenat(osnovica, vrsta.StopaPoreza),
            PioPrimalac = Procenat(osnovica, vrsta.StopaPioPrimalac),
            ZdravstvoPrimalac = Procenat(osnovica, vrsta.StopaZdravstvoPrimalac),
            NezaposlenostPrimalac = Procenat(osnovica, vrsta.StopaNezaposlenostPrimalac),
            PioIsplatilac = Procenat(osnovica, vrsta.StopaPioIsplatilac),
            ZdravstvoIsplatilac = Procenat(osnovica, vrsta.StopaZdravstvoIsplatilac),
            NezaposlenostIsplatilac = Procenat(osnovica, vrsta.StopaNezaposlenostIsplatilac)
        };
    }

    /// <summary>
    /// Bruto naknada iz ugovorenog neto iznosa.
    ///
    /// U praksi se ugovara „na ruke", a od bruta zavise i porez i doprinosi, pa preračun
    /// mora da pogodi neto <b>tačno</b>. Analitički izraz daje bruto na dinar, ali
    /// zaokrugljivanje svake stavke ume da pomeri neto za paru — zato se rezultat dotera
    /// koracima od jedne pare, u oba smera.
    /// </summary>
    public static decimal BrutoIzNeta(VrstaUgovora vrsta, decimal neto)
    {
        ArgumentNullException.ThrowIfNull(vrsta);

        neto = Math.Round(neto, 2, MidpointRounding.AwayFromZero);
        if (neto <= 0) return 0m;

        // neto = bruto − (bruto − normirani) × (stopa poreza + stope na teret primaoca)
        decimal deoOsnovice = 1m - vrsta.NormiraniTroskoviProcenat / 100m;
        decimal opterecenje = (vrsta.StopaPoreza + vrsta.ZbirStopaPrimaoca) / 100m;
        decimal faktor = 1m - deoOsnovice * opterecenje;

        if (faktor <= 0m)
        {
            throw new InvalidOperationException(
                $"Zbir stopa vrste ugovora „{vrsta.Naziv}\" ne ostavlja ništa za isplatu — " +
                "preračun neta u bruto nije moguć. Proverite stope u šifarniku vrsta ugovora.");
        }

        decimal bruto = Math.Round(neto / faktor, 2, MidpointRounding.AwayFromZero);

        // Doterivanje: najviše nekoliko koraka, jer je polazna vrednost tačna do pare.
        for (int korak = 0; korak < 200; korak++)
        {
            decimal razlika = neto - Izracunaj(vrsta, bruto).Neto;
            if (razlika == 0m) break;

            bruto += razlika > 0m ? 0.01m : -0.01m;
        }

        return bruto;
    }

    private static decimal Procenat(decimal osnovica, decimal stopa)
        => stopa == 0m ? 0m : Math.Round(osnovica * stopa / 100m, 2, MidpointRounding.AwayFromZero);

    // ── Upis obračuna ────────────────────────────────────────────────

    /// <summary>
    /// Obračunava naknadu po ugovoru i upisuje je kao obračun vezan za datu isplatu.
    /// </summary>
    /// <param name="iznos">
    /// Iznos koji se isplaćuje. Tumači se kao neto ili bruto po <paramref name="iznosJeNeto"/>,
    /// a ne po tome kako je ugovor zaključen — jedan ugovor može da se isplati u ratama koje
    /// se dogovaraju svaka za sebe.
    /// </param>
    public RezultatUgovora Obracunaj(int ugovorId, int isplataId, decimal iznos, bool iznosJeNeto)
    {
        var ugovor = _db.Ugovori
            .Include(u => u.VrstaUgovora)
            .FirstOrDefault(u => u.UgovorId == ugovorId);

        if (ugovor == null)
            return new RezultatUgovora { Poruka = "Ugovor nije pronađen." };

        var isplata = _db.Isplate.FirstOrDefault(i => i.IsplataId == isplataId);
        if (isplata == null)
            return new RezultatUgovora { Poruka = "Isplata nije pronađena." };

        // Naknada ide isključivo na isplatu svog roda. Na isplati zarade bi joj obračunski
        // period postao mesec ZA KOJI se zarada isplaćuje, a njen period je mesec isplate —
        // dva perioda ne staju u jedno polje 1.2, pa bi prijava bila pogrešna.
        if (!isplata.JeVanRadnogOdnosa)
        {
            return new RezultatUgovora
            {
                Poruka = $"„{isplata.Naziv}“ je isplata zarade i ne može nositi naknadu po ugovoru. " +
                         "Naknada se prijavljuje zasebnom prijavom, sa mesecom isplate kao obračunskim " +
                         "periodom — napravite isplatu naknada za datum kada honorar zaista ide na račun."
            };
        }

        if (iznos <= 0)
            return new RezultatUgovora { Poruka = "Iznos naknade mora biti veći od nule." };

        var vrsta = ugovor.VrstaUgovora;

        ObracunUgovora racunica;
        try
        {
            racunica = iznosJeNeto
                ? Izracunaj(vrsta, BrutoIzNeta(vrsta, iznos))
                : Izracunaj(vrsta, iznos);
        }
        catch (InvalidOperationException ex)
        {
            return new RezultatUgovora { Poruka = ex.Message };
        }

        var karton = ObezbediKarton(ugovor.BrojRadnika, isplata.Godina, isplata.Mesec);
        if (karton == null)
        {
            return new RezultatUgovora
            {
                Poruka = $"Za primaoca #{ugovor.BrojRadnika} ne postoji karton ni u jednom periodu. " +
                         "Unesite ga u „Radnici\" i označite kao lice van radnog odnosa."
            };
        }

        // Isti ugovor se u istoj isplati ne obračunava dvaput — to bi dalo dva reda za isto
        // lice u jednoj PPP-PD prijavi, što Poreska uprava odbija.
        var postojeci = _db.ObracuniPlata.FirstOrDefault(o =>
            o.UgovorId == ugovorId && o.IsplataId == isplataId && !o.Storniran);

        if (postojeci != null)
        {
            return new RezultatUgovora
            {
                Poruka = $"Ugovor je već obračunat u isplati „{isplata.Naziv}“. " +
                         "Obrišite ili stornirajte zatečeni obračun pre novog."
            };
        }

        var obracun = new ObracunPlate
        {
            RadnikId = karton.Id,
            Godina = isplata.Godina,
            Mesec = isplata.Mesec,
            IsplataId = isplata.IsplataId,
            UgovorId = ugovor.UgovorId,
            DatumObracuna = DateTime.Now,
            Napomena = Skrati(ugovor.Predmet, 200),

            BrutoZarada = racunica.Bruto,
            PoreskaOsnovica = racunica.Osnovica,
            OsnovicaDoprinosa = racunica.Osnovica,
            PorezNaDohodak = racunica.Porez,

            DoprinosPioRadnik = racunica.PioPrimalac,
            DoprinosZdravstvoRadnik = racunica.ZdravstvoPrimalac,
            DoprinosNezaposlenostRadnik = racunica.NezaposlenostPrimalac,

            DoprinosPioPoslodavac = racunica.PioIsplatilac,
            DoprinosZdravstvoPoslodavac = racunica.ZdravstvoIsplatilac,
            DoprinosNezaposlenostPoslodavac = racunica.NezaposlenostIsplatilac,

            NetoIsplata = racunica.Neto
        };

        _db.ObracuniPlata.Add(obracun);
        _db.SaveChanges();

        AuditService.ZabeleziZaRadnika(
            _db, isplata.Godina, isplata.Mesec, ugovor.BrojRadnika, karton.ImeIPrezime,
            AkcijaObracuna.Kreiran,
            $"Naknada po ugovoru ({vrsta.Naziv}) — bruto {racunica.Bruto:N2}, neto {racunica.Neto:N2}, " +
            $"isplata „{isplata.Naziv}“");

        return new RezultatUgovora
        {
            Uspesno = true,
            Obracun = obracun,
            Racunica = racunica,
            Poruka = $"Obračunata naknada po ugovoru: bruto {racunica.Bruto:N2}, neto {racunica.Neto:N2}."
        };
    }

    /// <summary>
    /// Karton primaoca za dati period; ako ga nema, prepisuje se poslednji raniji.
    ///
    /// Karton je periodičan zato što se podaci radnika menjaju kroz godinu, a primalac po
    /// ugovoru u većini meseci nema nikakvu isplatu — pa bi bez ovoga korisnik morao ručno da
    /// ga unosi svaki put kad se ugovor isplati.
    /// </summary>
    public Radnik? ObezbediKarton(int brojRadnika, int godina, int mesec)
    {
        var uPeriodu = _db.Radnici
            .FirstOrDefault(r => r.BrojRadnika == brojRadnika && r.Godina == godina && r.Mesec == mesec);

        if (uPeriodu != null) return uPeriodu;

        var poslednji = _db.Radnici
            .Where(r => r.BrojRadnika == brojRadnika
                        && (r.Godina < godina || (r.Godina == godina && r.Mesec <= mesec)))
            .OrderByDescending(r => r.Godina)
            .ThenByDescending(r => r.Mesec)
            .FirstOrDefault()
            ?? _db.Radnici
                .Where(r => r.BrojRadnika == brojRadnika)
                .OrderBy(r => r.Godina)
                .ThenBy(r => r.Mesec)
                .FirstOrDefault();

        if (poslednji == null) return null;

        // Kopija mora biti VERNA, a ne samo dovoljna za isplatu naknade. Otkako i zaposleni
        // sme biti primalac po ugovoru, ovaj karton može biti prvi zapis tog lica u mesecu —
        // i onaj koji obračun zarade posle zatekne. Osakaćena kopija bi mu tada dala nulti
        // koeficijent i pogrešnu platu.
        var novi = new Radnik
        {
            Godina = godina,
            Mesec = mesec,
            BrojRadnika = poslednji.BrojRadnika,
            ImeIPrezime = poslednji.ImeIPrezime,
            Jmbg = poslednji.Jmbg,
            Lbo = poslednji.Lbo,
            MaticniBroj = poslednji.MaticniBroj,
            DatumRodjenja = poslednji.DatumRodjenja,
            MestoRodjenja = poslednji.MestoRodjenja,
            AdresaStanovanja = poslednji.AdresaStanovanja,
            Mesto = poslednji.Mesto,
            SifraOpstine = poslednji.SifraOpstine,
            Email = poslednji.Email,
            DatumZaposlenja = poslednji.DatumZaposlenja,
            DatumPrestanka = poslednji.DatumPrestanka,
            Kategorija = poslednji.Kategorija,
            Radno_Mesto = poslednji.Radno_Mesto,
            BrojRadneJedinice = poslednji.BrojRadneJedinice,
            MinuliRadGodine = poslednji.MinuliRadGodine,
            Koeficijent = poslednji.Koeficijent,
            Koeficijent1 = poslednji.Koeficijent1,
            OsnovnaPlata = poslednji.OsnovnaPlata,
            StopaPio = poslednji.StopaPio,
            StopaZdravstvo = poslednji.StopaZdravstvo,
            StopaNezaposlenost = poslednji.StopaNezaposlenost,
            BankovniRacun = poslednji.BankovniRacun,
            NazivBanke = poslednji.NazivBanke,
            Aktivan = poslednji.Aktivan,
            VanRadnogOdnosa = poslednji.VanRadnogOdnosa,
            LicnoOslobodjenje = poslednji.LicnoOslobodjenje,
            Operativni = poslednji.Operativni,
            SifraMestaTroska = poslednji.SifraMestaTroska,
            DatumUnosa = DateTime.Now
        };

        _db.Radnici.Add(novi);
        _db.SaveChanges();
        return novi;
    }

    /// <summary>Koliko je puta i u kom ukupnom bruto iznosu isplaćeno po svakom ugovoru.</summary>
    public sealed record IsplacenoPoUgovoru(int BrojIsplata, decimal Bruto);

    /// <summary>
    /// Zbir isplaćenog po ugovorima, bez storniranih.
    ///
    /// Zbrajanje se namerno radi <b>u memoriji</b>, pošto se redovi pročitaju: SQLite ne ume
    /// <c>SUM</c> nad <c>decimal</c> kolonom, pa grupisanje na strani baze pada sa „cannot
    /// apply aggregate operator 'Sum' on expressions of type 'decimal'". Zato ovo stoji ovde,
    /// u servisu — da postoji jedno mesto koje test pokriva nad pravim SQLite-om.
    /// </summary>
    public IReadOnlyDictionary<int, IsplacenoPoUgovoru> IsplacenoPoUgovorima()
        => _db.ObracuniPlata
            .AsNoTracking()
            .Where(o => o.UgovorId != null && !o.Storniran)
            .Select(o => new { UgovorId = o.UgovorId!.Value, o.BrutoZarada })
            .ToList()
            .GroupBy(o => o.UgovorId)
            .ToDictionary(g => g.Key, g => new IsplacenoPoUgovoru(g.Count(), g.Sum(o => o.BrutoZarada)));

    // ── Kontrolne provere ────────────────────────────────────────────

    /// <summary>
    /// Provere nad obračunima po ugovoru u datom periodu. Traže ono što prođe generisanje, a
    /// padne kod Poreske uprave ili ostavi primaoca bez novca.
    /// </summary>
    public IReadOnlyList<NalazProvere> Proveri(int godina, int mesec)
    {
        var nalazi = new List<NalazProvere>();

        List<ObracunPlate> obracuni;
        try
        {
            obracuni = _db.ObracuniPlata
                .AsNoTracking()
                .Include(o => o.Radnik)
                .Include(o => o.Ugovor!).ThenInclude(u => u.VrstaUgovora)
                .Where(o => o.Godina == godina && o.Mesec == mesec && o.UgovorId != null && !o.Storniran)
                .ToList();
        }
        catch
        {
            return nalazi;   // baza starije verzije nema tabelu ugovora
        }

        foreach (var o in obracuni)
        {
            var vrsta = o.Ugovor?.VrstaUgovora;
            if (vrsta == null) continue;

            string ime = o.Radnik?.ImeIPrezime ?? $"(primalac #{o.Ugovor!.BrojRadnika})";

            // Bez OVP oznake nema šifre vrste prihoda, a prijava bez nje biva odbijena.
            if (string.IsNullOrWhiteSpace(vrsta.Ovp))
            {
                nalazi.Add(new NalazProvere
                {
                    Tezina = TezinaNalaza.Greska,
                    BrojRadnika = o.Radnik?.BrojRadnika,
                    Radnik = ime,
                    Provera = "Vrsta ugovora bez oznake vrste prihoda",
                    Opis = $"„{vrsta.Naziv}“ nema OVP oznaku, pa se šifra vrste prihoda ne može sastaviti. " +
                           "Upišite je iz važećeg Kataloga vrste prihoda u šifarnik vrsta ugovora."
                });
            }

            // Primalac po ugovoru najčešće nije zaposlen, pa se lako previdi da mu karton
            // nije označen — a onda ga obračun zarade zahvata i traži od njega sate i fond.
            //
            // Tipovi 01 i 02 su izuzetak, i to propisan: zaposleni i osnivač zaposlen u svom
            // društvu SMEJU biti isplaćeni po ugovoru, i tada su legitimno i radnici. Za njih
            // oznaka ne treba, pa bi nalaz bio netačan — a netačno upozorenje nauči korisnika
            // da nalaze preskače.
            bool smeBitiZaposlen = o.Ugovor!.TipPrimaoca
                is TipPrimaocaPrihoda.Zaposleni or TipPrimaocaPrihoda.OsnivacZaposlenUSvomDrustvu;

            if (o.Radnik is { VanRadnogOdnosa: false } && !smeBitiZaposlen)
            {
                nalazi.Add(new NalazProvere
                {
                    Tezina = TezinaNalaza.Upozorenje,
                    BrojRadnika = o.Radnik.BrojRadnika,
                    Radnik = ime,
                    Provera = "Primalac nije označen kao lice van radnog odnosa",
                    Opis = "Karton nije označen poljem „Van radnog odnosa\", pa ga ekrani zarade i " +
                           "dalje nude za obračun plate."
                });
            }
        }

        return nalazi;
    }

    private static string Skrati(string tekst, int maxDuzina)
        => string.IsNullOrEmpty(tekst) || tekst.Length <= maxDuzina ? tekst : tekst[..maxDuzina];
}
