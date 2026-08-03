using System;
using System.Collections.Generic;
using System.Linq;
using ERPiZaradeData;
using ERPiZaradeData.Models;

namespace ERPiZaradeApp.Services;

/// <summary>
/// Upis revizionog traga nad obračunima. Isti obrazac kao <c>NalogAudit</c> u ERPiFinansije,
/// s tim što je ovde materijalna odgovornost veća — zato se beleže i zaključavanje i
/// otključavanje, a ne samo brisanje.
///
/// Upis traga nikada ne sme da obori radnju koju prati: ako se trag ne može upisati,
/// greška se guta. Bolje je izgubiti zapis nego ostaviti obračun u polovičnom stanju.
/// </summary>
public static class AuditService
{
    /// <summary>Beleži radnju nad celim obračunskim periodom.</summary>
    public static void Zabelezi(
        PlataDbContext db,
        int godina,
        int mesec,
        AkcijaObracuna akcija,
        string? detalji = null)
        => Upisi(db, [Napravi(godina, mesec, akcija, null, null, detalji)]);

    /// <summary>Beleži radnju nad obračunom jednog radnika.</summary>
    public static void ZabeleziZaRadnika(
        PlataDbContext db,
        int godina,
        int mesec,
        int brojRadnika,
        string? imeRadnika,
        AkcijaObracuna akcija,
        string? detalji = null)
        => Upisi(db, [Napravi(godina, mesec, akcija, brojRadnika, imeRadnika, detalji)]);

    private static ObracunAudit Napravi(
        int godina,
        int mesec,
        AkcijaObracuna akcija,
        int? brojRadnika,
        string? imeRadnika,
        string? detalji)
    {
        var korisnik = AppSession.TrenutniKorisnik;
        return new ObracunAudit
        {
            Godina = godina,
            Mesec = mesec,
            BrojRadnika = brojRadnika,
            ImeRadnika = Skrati(imeRadnika, 60),
            Akcija = akcija,
            KorisnikId = korisnik?.Id,
            KorisnickoIme = korisnik?.KorisnickoIme,
            Detalji = Skrati(detalji, 300),
            Vreme = DateTime.Now
        };
    }

    private static void Upisi(PlataDbContext db, IEnumerable<ObracunAudit> zapisi)
    {
        try
        {
            db.ObracunAuditi.AddRange(zapisi);
            db.SaveChanges();
        }
        catch
        {
            // Namerno prazno — vidi napomenu uz klasu.
        }
    }

    private static string? Skrati(string? tekst, int maxDuzina)
        => string.IsNullOrEmpty(tekst) || tekst.Length <= maxDuzina
            ? tekst
            : tekst[..maxDuzina];

    /// <summary>Čitljiv opis radnje za prikaz u izveštaju.</summary>
    public static string OpisAkcije(AkcijaObracuna akcija) => akcija switch
    {
        AkcijaObracuna.Kreiran => "Kreiran obračun",
        AkcijaObracuna.Prekalkulisan => "Prekalkulisan obračun",
        AkcijaObracuna.Zakljucan => "Zaključan period",
        AkcijaObracuna.Otkljucan => "Otključan period",
        AkcijaObracuna.Obrisan => "Obrisan obračun",
        AkcijaObracuna.Storniran => "Storniran obračun",
        AkcijaObracuna.PppPdGenerisan => "Generisana PPP-PD prijava",
        AkcijaObracuna.IsplataDodata => "Dodata isplata u mesecu",
        AkcijaObracuna.IsplataObrisana => "Obrisana isplata u mesecu",
        _ => akcija.ToString()
    };
}
