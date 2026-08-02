using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using ERPiZaradeData;
using ERPiZaradeData.Models;

namespace ERPiZaradeApp.Services;

/// <summary>Ishod prevođenja jednog obračuna.</summary>
public sealed class PrevodNalaz
{
    public int Godina { get; init; }
    public int Mesec { get; init; }
    public int BrojRadnika { get; init; }
    public string Radnik { get; init; } = "";

    /// <summary>Nepokriveni deo bruto iznosa — koliko se zbir stavki razlikuje od ukupnog bruta.</summary>
    public decimal Razlika { get; init; }

    public string Opis { get; init; } = "";
}

/// <summary>Rezultat prevođenja, pre ili posle upisa.</summary>
public sealed class PrevodRezultat
{
    public int UkupnoObracuna { get; init; }
    public int VecImajuStavke { get; init; }
    public int Prevedeno { get; init; }

    /// <summary>Obračuni kod kojih se zbir stavki ne slaže sa bruto iznosom — nisu prevedeni.</summary>
    public IReadOnlyList<PrevodNalaz> Neslaganja { get; init; } = [];

    public bool JeCist => Neslaganja.Count == 0;
}

/// <summary>
/// Prevođenje zatečenih obračuna na model stavki (Faza 2.1, odluka „prevesti sve").
///
/// Legacy kolone nose bruto iznose uprkos nazivima koji počinju sa „Neto" — <c>NetoZar</c>
/// je bruto osnovne zarade, <c>NetoPrek</c> bruto prekovremenog i tako dalje. Naziv je
/// ostatak iz DBF-a i lako navodi na pogrešan zaključak.
///
/// Dve komponente — bolovanje preko 30 dana i porodiljsko odsustvo — imaju sačuvane sate,
/// ali nikad nisu dobile sopstvenu kolonu sa iznosom; ulazile su samo u ukupan bruto. One se
/// rekonstruišu istom formulom koju koristi obračun (sati × prosek).
///
/// Obračun kod kog se posle svega zbir stavki ne slaže sa bruto iznosom <b>ne prevodi se</b>
/// i prijavljuje se poimenično. Delimično preveden obračun izgleda ispravno, a daje pogrešan
/// listić.
/// </summary>
public class PrevodStavkiService
{
    /// <summary>
    /// Dozvoljeno odstupanje po obračunu. Osamnaest komponenti zaokruženih na dve decimale
    /// može da odstupi od jednom zaokruženog zbira za nekoliko para; sve preko toga je
    /// stvarna rupa u podacima, a ne zaokruživanje.
    /// </summary>
    private const decimal DozvoljenoOdstupanje = 0.50m;

    private readonly PlataDbContext _db;

    public PrevodStavkiService(PlataDbContext db) => _db = db;

    /// <summary>Prikazuje šta bi prevođenje uradilo, bez upisa.</summary>
    public PrevodRezultat Proveri(int? godina = null) => Izvrsi(godina, upisi: false);

    /// <summary>Prevodi i upisuje stavke za obračune koji se slažu.</summary>
    public PrevodRezultat Prevedi(int? godina = null) => Izvrsi(godina, upisi: true);

    private PrevodRezultat Izvrsi(int? godina, bool upisi)
    {
        var upit = _db.ObracuniPlata
            .Include(o => o.Radnik)
            .Include(o => o.Stavke)
            .AsQueryable();

        if (godina.HasValue) upit = upit.Where(o => o.Godina == godina.Value);

        var obracuni = upit.ToList();

        var sifarnik = _db.VrstePrimanja
            .AsNoTracking()
            .ToDictionary(v => v.Sifra, v => v.VrstaPrimanjaId, StringComparer.Ordinal);

        if (sifarnik.Count == 0)
        {
            return new PrevodRezultat
            {
                UkupnoObracuna = obracuni.Count,
                Neslaganja =
                [
                    new PrevodNalaz { Opis = "Šifarnik vrsta primanja je prazan — nema u šta da se prevede." }
                ]
            };
        }

        var neslaganja = new List<PrevodNalaz>();
        int vecImaju = 0;
        int prevedeno = 0;

        foreach (var obracun in obracuni)
        {
            if (obracun.Stavke.Count > 0)
            {
                vecImaju++;
                continue;
            }

            var stavke = IzvediStavke(obracun, sifarnik);
            decimal zbir = stavke.Sum(s => s.Iznos);

            // `Neto` kolona nosi ukupan bruto iznos — vidi napomenu uz klasu.
            decimal ukupanBruto = obracun.Neto > 0 ? obracun.Neto : obracun.UkupnoBruto;
            decimal razlika = ukupanBruto - zbir;

            if (Math.Abs(razlika) > DozvoljenoOdstupanje)
            {
                neslaganja.Add(new PrevodNalaz
                {
                    Godina = obracun.Godina,
                    Mesec = obracun.Mesec,
                    BrojRadnika = obracun.Radnik?.BrojRadnika ?? 0,
                    Radnik = obracun.Radnik?.ImeIPrezime ?? $"(radnik #{obracun.RadnikId})",
                    Razlika = razlika,
                    Opis = $"Zbir stavki {zbir:N2} ne pokriva bruto {ukupanBruto:N2}."
                });
                continue;
            }

            if (upisi)
            {
                foreach (var stavka in stavke) obracun.Stavke.Add(stavka);
            }

            prevedeno++;
        }

        if (upisi && prevedeno > 0) _db.SaveChanges();

        return new PrevodRezultat
        {
            UkupnoObracuna = obracuni.Count,
            VecImajuStavke = vecImaju,
            Prevedeno = prevedeno,
            Neslaganja = neslaganja
        };
    }

    /// <summary>Preslikava legacy kolone jednog obračuna u stavke.</summary>
    private static List<ObracunStavka> IzvediStavke(ObracunPlate o, Dictionary<string, int> sifarnik)
    {
        var stavke = new List<ObracunStavka>();

        void Dodaj(string sifra, decimal iznos, int sati = 0)
        {
            if (iznos == 0m && sati == 0) return;
            if (!sifarnik.TryGetValue(sifra, out int vrstaId)) return;

            stavke.Add(new ObracunStavka
            {
                VrstaPrimanjaId = vrstaId,
                Sati = sati,
                Iznos = Math.Round(iznos, 2)
            });
        }

        // Kolone „Neto*" nose BRUTO iznose — naziv je ostatak iz DBF-a.
        Dodaj(VrstePrimanjaSeed.OsnovnaZarada,    o.NetoZar,           o.RedovniSati);
        Dodaj(VrstePrimanjaSeed.MinuliRad,        o.BrutoMinuliRad);
        Dodaj(VrstePrimanjaSeed.Prekovremeni,     o.NetoPrek,          o.PrekovremeneSati);
        Dodaj(VrstePrimanjaSeed.NocniRad,         o.NetoNocni,         o.NocniSati);
        Dodaj(VrstePrimanjaSeed.RadPraznikom,     o.NetoDrza,          o.RadPraznikomSati);
        Dodaj(VrstePrimanjaSeed.NeradniPraznik,   o.NetoNerd,          o.DrzavniPraznikSati);
        Dodaj(VrstePrimanjaSeed.RadNedeljom,      o.NetoNede,          (int)o.NedeljaSati);
        Dodaj(VrstePrimanjaSeed.GodisnjiOdmor,    o.NetoGOd,           o.GodisnjioOdmorSati);
        Dodaj(VrstePrimanjaSeed.Bolovanje,        o.NetoBol,           o.BolovanjeSati);
        Dodaj(VrstePrimanjaSeed.Bolovanje100,     o.NetoB100,          (int)o.Bolovanje100SatiLegacy);
        Dodaj(VrstePrimanjaSeed.PlacenoOdsustvo,  o.NetoPlac,          (int)o.PlacenoOdsustvoSatiLegacy);
        Dodaj(VrstePrimanjaSeed.PlacenoZakonski,  o.NetoPlZ,           (int)o.PlacenoZakonskiSatiLegacy);
        Dodaj(VrstePrimanjaSeed.Stimulacija,      o.BrutoStimulacija);
        Dodaj(VrstePrimanjaSeed.TopliObrok,       o.NetoTo);
        Dodaj(VrstePrimanjaSeed.Regres,           o.NetoReg);
        Dodaj(VrstePrimanjaSeed.BrutoDodatak,     o.Varijabila);

        // Bolovanje preko 30 dana i porodiljsko nikad nisu dobili kolonu sa iznosom, iako
        // su ulazili u ukupan bruto. Rekonstruišu se formulom koju koristi i sam obračun.
        int satiPreko30 = (int)o.BolovanjePreko60SatiLegacy;
        if (satiPreko30 > 0)
            Dodaj(VrstePrimanjaSeed.BolovanjePreko30, satiPreko30 * o.Prosek, satiPreko30);

        int satiPorodiljsko = (int)o.PorodiljskoOdsustvoSatiLegacy;
        if (satiPorodiljsko > 0)
            Dodaj(VrstePrimanjaSeed.Porodiljsko, satiPorodiljsko * o.Prosek, satiPorodiljsko);

        return stavke;
    }
}
