using System.Collections.Generic;
using ERPiZaradeData.Models;

namespace ERPiZaradeData;

/// <summary>
/// Polazni sadržaj šifarnika poreskih olakšica.
///
/// Ovo su <b>primeri, ne propis</b>. OL oznake i procenti se menjaju izmenama Zakona o porezu
/// na dohodak građana i Kataloga vrste prihoda, pa ih pre upotrebe treba proveriti u važećem
/// katalogu Poreske uprave i ispraviti u šifarniku — bez izmene koda.
///
/// Ništa se ne primenjuje samo od sebe: olakšica deluje tek kada radnik u kartonu nosi
/// odgovarajuću OL oznaku u SVP šifri.
/// </summary>
public static class PoreskeOlaksiceSeed
{
    private const string Proveriti = "Proveriti oznaku i procenat u važećem Katalogu vrste prihoda.";

    public static List<PoreskaOlaksica> Podrazumevane() =>
    [
        // Član 21v — poslodavac plati pun iznos, pa podnosi Obrazac PPD za povraćaj.
        Povracaj("08", "Novozaposleno lice — povraćaj 65%", "čl. 21v ZPDG", 65m),
        Povracaj("09", "Novozaposleno lice — povraćaj 70%", "čl. 21v ZPDG", 70m),
        Povracaj("10", "Novozaposleno lice — povraćaj 75%", "čl. 21v ZPDG", 75m),

        // Oslobođenja — u PPP-PD se iskazuju umanjeni iznosi, uz MFP deklaraciju.
        Oslobodjenje("24", "Kvalifikovano novozaposleno lice", "čl. 21ž ZPDG", 70m, 100m),
        Oslobodjenje("32", "Osnivač inovativnog preduzeća", "čl. 21đ ZPDG", 100m, 100m)
    ];

    private static PoreskaOlaksica Povracaj(string sifra, string naziv, string osnov, decimal procenat)
        => new()
        {
            Sifra = sifra,
            Naziv = naziv,
            PravniOsnov = osnov,
            Mehanizam = MehanizamOlaksice.Povracaj,
            ProcenatPoreza = procenat,
            ProcenatDoprinosa = procenat,
            Aktivna = true,
            Napomena = Proveriti
        };

    /// <summary>
    /// Oslobođenje bez MFP deklaracije se neće prijaviti, pa na to upozoravaju kontrolne
    /// provere — koja oznaka MFP nosi umanjenje zavisi od SVP šifre i mora se uneti ručno.
    /// </summary>
    private static PoreskaOlaksica Oslobodjenje(
        string sifra, string naziv, string osnov, decimal procenatPoreza, decimal procenatDoprinosa)
        => new()
        {
            Sifra = sifra,
            Naziv = naziv,
            PravniOsnov = osnov,
            Mehanizam = MehanizamOlaksice.Oslobodjenje,
            ProcenatPoreza = procenatPoreza,
            ProcenatDoprinosa = procenatDoprinosa,
            Aktivna = true,
            Napomena = Proveriti + " Uneti i MFP deklaraciju, inače se umanjenje neće prijaviti."
        };
}
