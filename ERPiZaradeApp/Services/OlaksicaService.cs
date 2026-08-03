using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using ERPiZaradeData;
using ERPiZaradeData.Models;

namespace ERPiZaradeApp.Services;

/// <summary>Olakšica primenjena na jedan obračun, sa već izračunatim iznosima.</summary>
public sealed class PrimenjenaOlaksica
{
    public required PoreskaOlaksica Olaksica { get; init; }
    public required decimal Porez { get; init; }
    public required decimal Doprinosi { get; init; }

    /// <summary>Oslobođenje umanjuje ono što se plaća; povraćaj ne dira nijedan iznos.</summary>
    public bool UmanjujeUplatu => Olaksica.Mehanizam == MehanizamOlaksice.Oslobodjenje;
}

/// <summary>
/// Utvrđivanje i primena poreske olakšice.
///
/// Olakšica se prepoznaje po <b>OL oznaci na pozicijama 7–8 SVP šifre</b> koju radnik već nosi
/// u kartonu — nema zasebnog polja, pa nema ni prilike da se to dvoje raziđe.
///
/// Dva mehanizma se ne smeju pomešati: kod <see cref="MehanizamOlaksice.Oslobodjenje"/> se
/// umanjuje ono što se plaća i to se prijavljuje kroz MFP; kod
/// <see cref="MehanizamOlaksice.Povracaj"/> se plaća pun iznos pa se posebnim zahtevom
/// (Obrazac PPD) traži povraćaj. Zamena bi značila da firma ili plati manje nego što sme,
/// ili traži povraćaj koji joj ne sleduje.
/// </summary>
public class OlaksicaService
{
    private readonly PlataDbContext _db;

    public OlaksicaService(PlataDbContext db) => _db = db;

    /// <summary>Izvlači OL oznaku iz SVP šifre; prazno kad je nema ili je „00" (bez olakšice).</summary>
    public static string OznakaIzSvp(string? svp)
    {
        if (!SvpService.JeSvpSifra(svp)) return "";

        string oznaka = svp![6..8];
        return oznaka == "00" ? "" : oznaka;
    }

    /// <summary>
    /// Utvrđuje olakšicu za obračun i računa iznose. Vraća <c>null</c> kada olakšice nema,
    /// kada je istekla ili kada osnovica nije pozitivna.
    /// </summary>
    /// <param name="porez">Obračunat porez pre olakšice.</param>
    /// <param name="doprinosiRadnika">Zbir doprinosa na teret radnika pre olakšice.</param>
    public PrimenjenaOlaksica? Utvrdi(
        Radnik radnik, string svp, int godina, int mesec, decimal porez, decimal doprinosiRadnika)
    {
        string oznaka = OznakaIzSvp(svp);
        if (oznaka.Length == 0) return null;

        PoreskaOlaksica? olaksica;
        try
        {
            olaksica = _db.PoreskeOlaksice
                .AsNoTracking()
                .Include(o => o.MfpDeklaracije)
                .FirstOrDefault(o => o.Sifra == oznaka && o.Aktivna);
        }
        catch
        {
            // Baza starije verzije još nema šifarnik olakšica — obračun radi kao i pre.
            return null;
        }

        if (olaksica == null) return null;

        var pocetak = new DateTime(godina, mesec, 1);
        var kraj = pocetak.AddMonths(1).AddDays(-1);

        // Oba roka moraju da pokriju period: rok propisa iz šifarnika i rok radnika iz kartona.
        if (olaksica.VaziOd.HasValue && olaksica.VaziOd.Value > kraj) return null;
        if (olaksica.VaziDo.HasValue && olaksica.VaziDo.Value < pocetak) return null;
        if (radnik.OlaksicaVaziDo.HasValue && radnik.OlaksicaVaziDo.Value < pocetak) return null;

        // Procenat sa kartona radnika ima prednost — kod nekih olakšica se određuje po licu.
        decimal procenatPoreza = radnik.ProcenatPovracajaPoreza > 0
            ? radnik.ProcenatPovracajaPoreza
            : olaksica.ProcenatPoreza;

        decimal procenatDoprinosa = radnik.ProcenatPovracajaDoprinosa > 0
            ? radnik.ProcenatPovracajaDoprinosa
            : olaksica.ProcenatDoprinosa;

        decimal iznosPoreza = Math.Round(Math.Max(0m, porez) * procenatPoreza / 100m, 2);
        decimal iznosDoprinosa = Math.Round(Math.Max(0m, doprinosiRadnika) * procenatDoprinosa / 100m, 2);

        if (iznosPoreza == 0m && iznosDoprinosa == 0m) return null;

        return new PrimenjenaOlaksica
        {
            Olaksica = olaksica,
            Porez = iznosPoreza,
            Doprinosi = iznosDoprinosa
        };
    }

    /// <summary>
    /// Vrednost koja se upisuje u MFP polje, prema izvoru izabranom u šifarniku.
    /// </summary>
    public static decimal VrednostMfp(
        OlaksicaMfp deklaracija,
        PrimenjenaOlaksica primenjena,
        decimal osnovicaPoreza,
        decimal osnovicaDoprinosa)
        => deklaracija.Izvor switch
        {
            IzvorMfp.UmanjenjePoreza => primenjena.Porez,
            IzvorMfp.UmanjenjeDoprinosa => primenjena.Doprinosi,
            IzvorMfp.OsnovicaPoreza => osnovicaPoreza,
            IzvorMfp.OsnovicaDoprinosa => osnovicaDoprinosa,
            IzvorMfp.ProcenatOlaksice => primenjena.Olaksica.ProcenatPoreza,
            IzvorMfp.FiksnaVrednost => deklaracija.FiksnaVrednost,
            _ => 0m
        };
}
