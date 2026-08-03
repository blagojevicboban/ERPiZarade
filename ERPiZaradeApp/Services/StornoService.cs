using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using ERPiZaradeData;
using ERPiZaradeData.Models;

namespace ERPiZaradeApp.Services;

/// <summary>Ishod storniranja ili njegovog poništavanja.</summary>
public class RezultatStorniranja
{
    public bool Uspesno { get; init; }

    /// <summary>Broj obračuna nad kojima je radnja izvršena.</summary>
    public int BrojObracuna { get; init; }

    /// <summary>Broj kredita kojima je rata vraćena odnosno ponovo skinuta.</summary>
    public int BrojKredita { get; init; }

    public string Poruka { get; init; } = "";
}

/// <summary>
/// Storniranje obračuna (Faza 2.7).
///
/// Do sada je jedini put za grešku u zaključanom periodu bio otključavanje, čime se izmeni
/// izlažu i svi ostali obračuni tog meseca. Storniranje poništava <b>jedan</b> obračun, i to
/// bez otključavanja perioda.
///
/// Stornirani obračun se <b>ne briše i ne nulira</b> — iznosi ostaju vidljivi, jer je to i
/// dalje ono što je jednom obračunato i, po pravilu, već prijavljeno. Ono što se menja jeste
/// da ga isplate i prijave više ne obuhvataju: nalozi za prenos, platni listići, PPP-PD i
/// PPP-PO ga preskaču.
///
/// Rata kredita se vraća, jer stornirani obračun nije isplaćen; ako bi ostala skinuta,
/// radnikov dug bi se smanjio bez ijednog dinara koji je otišao poveriocu.
/// </summary>
public class StornoService
{
    private readonly PlataDbContext _db;

    public StornoService(PlataDbContext db) => _db = db;

    /// <summary>
    /// Stornira obračune izabranog perioda; uz <paramref name="brojRadnika"/> samo obračun
    /// tog radnika, bez njega ceo period.
    /// </summary>
    /// <param name="razlog">
    /// Obavezan. Bez razloga se posle mesecima ne zna zašto obračuna nema u prijavi, a
    /// upravo to je pitanje koje se postavlja pri kontroli.
    /// </param>
    /// <param name="isplata">
    /// Isplata čiji se obračuni storniraju (Faza 2.2). <c>null</c> je ceo period. Bez ovog
    /// obuhvata bi storniranje pogrešne akontacije oborilo i konačnu isplatu istog meseca.
    /// </param>
    public RezultatStorniranja Storniraj(
        int godina, int mesec, int? brojRadnika, string razlog, Isplata? isplata = null)
        => Primeni(godina, mesec, brojRadnika, razlog, storniraj: true, isplata);

    /// <summary>
    /// Poništava storniranje i vraća obračun među važeće. Rata kredita se ponovo skida,
    /// da stanje bude isto kao pre storniranja.
    /// </summary>
    public RezultatStorniranja PonistiStorniranje(
        int godina, int mesec, int? brojRadnika, string razlog, Isplata? isplata = null)
        => Primeni(godina, mesec, brojRadnika, razlog, storniraj: false, isplata);

    private RezultatStorniranja Primeni(
        int godina, int mesec, int? brojRadnika, string razlog, bool storniraj, Isplata? isplata)
    {
        if (string.IsNullOrWhiteSpace(razlog))
        {
            return new RezultatStorniranja
            {
                Uspesno = false,
                Poruka = "Razlog je obavezan — bez njega se kasnije ne zna zašto obračun nije isplaćen."
            };
        }

        var upit = IsplataService.Obuhvat(
            _db.ObracuniPlata.Include(o => o.Radnik), godina, mesec, isplata);

        if (brojRadnika.HasValue)
            upit = upit.Where(o => o.Radnik.BrojRadnika == brojRadnika.Value);

        var obracuni = upit.Where(o => o.Storniran != storniraj).ToList();

        if (obracuni.Count == 0)
        {
            return new RezultatStorniranja
            {
                Uspesno = false,
                Poruka = storniraj
                    ? "Nema obračuna za storniranje — svi izabrani su već stornirani ili ih nema."
                    : "Nema storniranih obračuna za poništavanje u izabranom obuhvatu."
            };
        }

        int pogodjenihKredita = 0;
        DateTime sada = DateTime.Now;

        foreach (var o in obracuni)
        {
            o.Storniran = storniraj;
            o.DatumStorniranja = storniraj ? sada : null;
            o.RazlogStorniranja = storniraj ? Skrati(razlog, 200) : "";

            pogodjenihKredita += storniraj
                ? KreditRateService.VratiRate(_db, o)
                : KreditRateService.SkiniRate(_db, o);

            _db.Entry(o).State = EntityState.Modified;
        }

        _db.SaveChanges();

        // Radnja nad jednim radnikom se beleži imenom; radnja nad periodom obimom. Kad mesec
        // ima više isplata, u tragu mora stajati i koja — inače se ne zna koji je novac
        // poništen.
        string obuhvat = isplata == null || isplata.JePrva ? "" : $"Isplata: {isplata.Naziv}. ";

        string detalji = storniraj
            ? $"{obuhvat}Razlog: {razlog}"
            : $"{obuhvat}Poništeno storniranje. Razlog: {razlog}";

        if (brojRadnika.HasValue)
        {
            var prvi = obracuni[0];
            AuditService.ZabeleziZaRadnika(_db, godina, mesec, brojRadnika.Value,
                prvi.Radnik?.ImeIPrezime, AkcijaObracuna.Storniran, detalji);
        }
        else
        {
            AuditService.Zabelezi(_db, godina, mesec, AkcijaObracuna.Storniran,
                $"{obracuni.Count} obračuna. {detalji}");
        }

        return new RezultatStorniranja
        {
            Uspesno = true,
            BrojObracuna = obracuni.Count,
            BrojKredita = pogodjenihKredita,
            Poruka = storniraj
                ? $"Stornirano {obracuni.Count} obračuna; vraćeno rata kredita: {pogodjenihKredita}."
                : $"Poništeno storniranje za {obracuni.Count} obračuna; ponovo skinuto rata: {pogodjenihKredita}."
        };
    }

    private static string Skrati(string tekst, int maxDuzina)
        => tekst.Length <= maxDuzina ? tekst : tekst[..maxDuzina];
}
