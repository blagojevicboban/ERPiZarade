using System.Collections.Generic;
using ERPiZaradeData.Models;

namespace ERPiZaradeData;

/// <summary>
/// Podrazumevani sadržaj šifarnika vrsta ugovora van radnog odnosa (Faza 2.3).
///
/// Brojevi su ono što propis menja, pa su ovde samo <b>početna vrednost</b>: šifarnik se
/// održava iz programa i izmena stopa ne traži novu verziju. Vrednosti odgovaraju stanju
/// propisa u 2026: porez na druge prihode 20%, PIO 24%, zdravstveno 10,3% kad se plaća;
/// naknada za privremene i povremene poslove se oporezuje kao zarada (porez 10%, doprinosi
/// podeljeni na primaoca i isplatioca).
///
/// <see cref="VrstaUgovora.Ovp"/> nosi <b>samo</b> oznaku vrste prihoda; ceo devetocifreni
/// broj se sastavlja pri obračunu, jer zavisi i od statusa osiguranja primaoca. Gde OVP nije
/// potvrđen iz Kataloga vrste prihoda, polje je ostavljeno prazno i kontrolne provere na
/// njega upozoravaju — pogrešna šifra prolazi generisanje, a pada tek kod Poreske uprave.
/// </summary>
public static class VrsteUgovoraSeed
{
    // ── Šifre koje kod traži po imenu ────────────────────────────────
    public const string UgovorODelu = "UOD";
    public const string NaknadaOdboru = "ODB";
    public const string Autorski50 = "AUT50";
    public const string Autorski43 = "AUT43";
    public const string Autorski34 = "AUT34";
    public const string PrivremeniPoslovi = "PPP";
    public const string PrivremeniZadruga = "PPZ";

    /// <summary>Porez na druge prihode (čl. 85 ZPDG).</summary>
    private const decimal PorezDrugiPrihodi = 20.00m;

    /// <summary>Porez na zaradu — privremeni i povremeni poslovi se oporezuju kao zarada.</summary>
    private const decimal PorezZarada = 10.00m;

    private const decimal PioUkupno = 24.00m;
    private const decimal PioRadnik = 14.00m;
    private const decimal PioPoslodavac = 10.00m;
    private const decimal ZdravstvoVanRadnogOdnosa = 10.30m;
    private const decimal ZdravstvoRadnik = 5.15m;
    private const decimal ZdravstvoPoslodavac = 5.15m;
    private const decimal Nezaposlenost = 0.75m;

    /// <summary>Konto naknada po ugovoru; menja se u šifarniku ako firma koristi drugu analitiku.</summary>
    private const string KontoNaknadeUgovor = "526";

    private const string NapomenaOvp =
        "Proveriti OVP i tip primaoca u važećem Katalogu vrste prihoda pre prve prijave.";

    public static List<VrstaUgovora> Podrazumevane() =>
    [
        // ── Ugovor o delu i srodne naknade (OVP 601/602/603) ─────────
        // Katalog razlikuje status osiguranja kroz sam OVP, ali ista tri broja opisuju
        // i naknade članovima organa uprave, poslanicima i odbornicima, sudskim veštacima.
        new()
        {
            Sifra = UgovorODelu,
            Naziv = "Ugovor o delu",
            Ovp = "601",
            NormiraniTroskoviProcenat = 20.00m,
            StopaPoreza = PorezDrugiPrihodi,
            StopaPioPrimalac = PioUkupno,
            StopaZdravstvoPrimalac = 0m,
            Konto = KontoNaknadeUgovor,
            SifraPlacanja = "",
            Redosled = 10,
            Napomena = "Primalac osiguran po drugom osnovu. Za neosigurano lice koristi se OVP 602 " +
                       "uz zdravstveno 10,30%, a uz rešenje Fonda PIO o prestanku plaćanja doprinosa OVP 603."
        },
        new()
        {
            Sifra = "UOD2",
            Naziv = "Ugovor o delu — primalac bez osiguranja",
            Ovp = "602",
            NormiraniTroskoviProcenat = 20.00m,
            StopaPoreza = PorezDrugiPrihodi,
            StopaPioPrimalac = PioUkupno,
            StopaZdravstvoPrimalac = ZdravstvoVanRadnogOdnosa,
            Konto = KontoNaknadeUgovor,
            Redosled = 20,
            Napomena = "Lice koje nije osigurano po drugom osnovu — plaća se i zdravstveno osiguranje."
        },
        new()
        {
            Sifra = "UOD3",
            Naziv = "Ugovor o delu — prestanak plaćanja doprinosa",
            Ovp = "603",
            NormiraniTroskoviProcenat = 20.00m,
            StopaPoreza = PorezDrugiPrihodi,
            Konto = KontoNaknadeUgovor,
            Redosled = 30,
            Napomena = "Primenjuje se uz rešenje Fonda PIO o prestanku obaveze plaćanja doprinosa; " +
                       "obračunava se samo porez."
        },
        new()
        {
            Sifra = NaknadaOdboru,
            Naziv = "Naknada članovima upravnog i nadzornog odbora",
            Ovp = "601",
            NormiraniTroskoviProcenat = 20.00m,
            StopaPoreza = PorezDrugiPrihodi,
            StopaPioPrimalac = PioUkupno,
            Konto = KontoNaknadeUgovor,
            Redosled = 40,
            Napomena = "Katalog ove naknade svrstava uz ugovor o delu (OVP 601/602/603) — " +
                       "šifra zavisi od statusa osiguranja člana odbora."
        },

        // ── Autorske naknade (OVP 301–323) ───────────────────────────
        new()
        {
            Sifra = Autorski50,
            Naziv = "Autorska naknada — normirani troškovi 50%",
            Ovp = "301",
            NormiraniTroskoviProcenat = 50.00m,
            StopaPoreza = PorezDrugiPrihodi,
            StopaPioPrimalac = PioUkupno,
            Konto = KontoNaknadeUgovor,
            Redosled = 110,
            Napomena = "Primalac osiguran po drugom osnovu. Za neosigurano lice OVP je 302, " +
                       "uz zdravstveno 10,30%."
        },
        new()
        {
            Sifra = Autorski43,
            Naziv = "Autorska naknada — normirani troškovi 43%",
            Ovp = "303",
            NormiraniTroskoviProcenat = 43.00m,
            StopaPoreza = PorezDrugiPrihodi,
            StopaPioPrimalac = PioUkupno,
            Konto = KontoNaknadeUgovor,
            Redosled = 120,
            Napomena = "Primalac osiguran po drugom osnovu. " + NapomenaOvp
        },
        new()
        {
            Sifra = Autorski34,
            Naziv = "Autorska naknada — normirani troškovi 34%",
            Ovp = "",
            NormiraniTroskoviProcenat = 34.00m,
            StopaPoreza = PorezDrugiPrihodi,
            StopaPioPrimalac = PioUkupno,
            Konto = KontoNaknadeUgovor,
            Redosled = 130,
            Napomena = "OVP nije potvrđen — upisati ga iz Kataloga vrste prihoda (opseg 301–323) " +
                       "pre prve prijave."
        },

        // ── Privremeni i povremeni poslovi (OVP 150–152) ─────────────
        // Ova naknada se po čl. 13 ZPDG smatra zaradom: nema normiranih troškova, porez je
        // 10% i doprinosi se dele na primaoca i isplatioca kao kod zaposlenog.
        new()
        {
            Sifra = PrivremeniPoslovi,
            Naziv = "Privremeni i povremeni poslovi",
            Ovp = "150",
            NormiraniTroskoviProcenat = 0m,
            StopaPoreza = PorezZarada,
            StopaPioPrimalac = PioRadnik,
            StopaZdravstvoPrimalac = ZdravstvoRadnik,
            StopaNezaposlenostPrimalac = Nezaposlenost,
            StopaPioIsplatilac = PioPoslodavac,
            StopaZdravstvoIsplatilac = ZdravstvoPoslodavac,
            Konto = KontoNaknadeUgovor,
            Redosled = 210,
            Napomena = "Ugovor zaključen neposredno sa poslodavcem. Naknada se oporezuje kao zarada, " +
                       "bez neoporezivog iznosa. " + NapomenaOvp
        },
        new()
        {
            Sifra = PrivremeniZadruga,
            Naziv = "Privremeni i povremeni poslovi preko zadruge",
            Ovp = "151",
            NormiraniTroskoviProcenat = 0m,
            StopaPoreza = PorezZarada,
            StopaPioPrimalac = PioRadnik,
            StopaZdravstvoPrimalac = ZdravstvoRadnik,
            StopaNezaposlenostPrimalac = Nezaposlenost,
            StopaPioIsplatilac = PioPoslodavac,
            StopaZdravstvoIsplatilac = ZdravstvoPoslodavac,
            Konto = KontoNaknadeUgovor,
            Redosled = 220,
            Napomena = "Ugovor zaključen preko omladinske ili studentske zadruge. Za lica mlađa od " +
                       "26 godina na redovnom školovanju Katalog predviđa OVP 152. " + NapomenaOvp
        }
    ];
}
