using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace ERPiZaradeApp.Services;

/// <summary>
/// Izvoz naloga u TXT format Hal E-Bank-a (platni promet u zemlji), koji koristi većina
/// poslovnih banaka u Srbiji.
///
/// Format je <b>fiksnih pozicija</b>, ne razdvojen znakom: svako polje ima tačan početak i
/// dužinu, red se završava sa CRLF. Fajl se sastoji od adresne stavke (tip 0), sabirne
/// stavke sa zbirom i brojem naloga (tip 9) i po jedne individualne stavke po nalogu (tip 1).
///
/// Specifikacija: Halcom, „Hal E-Bank — Platni promet u zemlji (format uvozno/izvoznih
/// datoteka)", verzija 17.x/20.x.
/// </summary>
public static class HalcomPpzWriter
{
    /// <summary>
    /// Kodni raspored fajla. Specifikacija ga ne navodi, a Hal E-Bank u regionu radi sa
    /// windows-1250 — pogrešan izbor ne obara uvoz, ali izobliči „č", „ć" i „đ" u imenima.
    /// Ako banka traži drugačiji, menja se samo ovde.
    /// </summary>
    private const int KodnaStrana = 1250;

    private const int DuzinaIndividualneStavke = 218;
    private const int DuzinaZaglavlja = 180;

    static HalcomPpzWriter()
    {
        // .NET Core ne nosi jednobajtne kodne strane u osnovnoj biblioteci — bez ove
        // registracije `GetEncoding(1250)` baca izuzetak.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    /// <summary>
    /// Sastavlja sadržaj TXT fajla. Nalazi koji bi doveli do odbijanja vraćaju se kroz
    /// <paramref name="nalazi"/>; izvoz se ne prekida, da se sve greške vide odjednom.
    /// </summary>
    public static byte[] Generisi(IReadOnlyList<NalogZaPrenos> nalozi, out IReadOnlyList<NalazProvere> nalazi)
    {
        var pronadjeni = new List<NalazProvere>();

        if (nalozi.Count == 0)
        {
            nalazi = pronadjeni;
            return [];
        }

        var prvi = nalozi[0];
        string racunPlatioca = NormalizujRacun(prvi.PlatilacRacun, prvi.PrimalacNaziv, "platioca", pronadjeni);
        decimal zbir = nalozi.Sum(n => n.Iznos);

        var sb = new StringBuilder();
        sb.Append(AdresnaStavka(racunPlatioca, prvi));
        sb.Append(SabirnaStavka(racunPlatioca, prvi, zbir, nalozi.Count));

        foreach (var nalog in nalozi)
            sb.Append(IndividualnaStavka(nalog, racunPlatioca, pronadjeni));

        nalazi = pronadjeni;
        return Encoding.GetEncoding(KodnaStrana).GetBytes(sb.ToString());
    }

    private static string AdresnaStavka(string racun, NalogZaPrenos prvi)
    {
        var red = new StringBuilder(new string(' ', DuzinaZaglavlja));
        Upisi(red, 1, 18, racun);
        Upisi(red, 19, 35, prvi.PlatilacNaziv);
        Upisi(red, 64, 6, prvi.DatumValute.ToString("ddMMyy", CultureInfo.InvariantCulture));
        Upisi(red, 168, 12, "MULTI E-BANK");
        Upisi(red, 180, 1, "0");   // tip stavke: adresna
        return red.ToString() + "\r\n";
    }

    private static string SabirnaStavka(string racun, NalogZaPrenos prvi, decimal zbir, int brojNaloga)
    {
        var red = new StringBuilder(new string(' ', DuzinaZaglavlja));
        Upisi(red, 1, 18, racun);
        Upisi(red, 19, 35, prvi.PlatilacNaziv);
        Upisi(red, 64, 15, Pare(zbir, 15));
        Upisi(red, 79, 5, brojNaloga.ToString(CultureInfo.InvariantCulture).PadLeft(5, '0'));
        Upisi(red, 180, 1, "9");   // tip stavke: sabirna
        return red.ToString() + "\r\n";
    }

    private static string IndividualnaStavka(NalogZaPrenos nalog, string racunPlatioca, List<NalazProvere> nalazi)
    {
        var red = new StringBuilder(new string(' ', DuzinaIndividualneStavke));

        Upisi(red, 1, 18, NormalizujRacun(nalog.PrimalacRacun, nalog.PrimalacNaziv, "primaoca", nalazi));
        Upisi(red, 19, 35, nalog.PrimalacNaziv);
        Upisi(red, 54, 35, IzdvojUlicu(nalog.PrimalacAdresa));
        Upisi(red, 89, 10, IzdvojMesto(nalog.PrimalacAdresa));
        Upisi(red, 99, 1, "0");

        // Zaduženje: poziv na broj se ne koristi kod isplate zarada.
        Upisi(red, 125, 36, nalog.Svrha);
        Upisi(red, 161, 5, "00000");

        // Šifra plaćanja je trocifrena („240" za zarade), ali se u fajl upisuje razdvojeno:
        // prva cifra je oblik plaćanja (2 = prenos), preostale dve idu u polje šifre.
        var (oblik, sifra) = RastaviSifruPlacanja(nalog.SifraPlacanja);
        Upisi(red, 167, 1, oblik);
        Upisi(red, 168, 2, sifra);

        Upisi(red, 172, 13, Pare(nalog.Iznos, 13));

        if (!string.IsNullOrWhiteSpace(nalog.PozivNaBroj))
        {
            Upisi(red, 185, 2, (nalog.ModelPozivaNaBroj ?? "").PadLeft(2, '0'));
            Upisi(red, 187, 23, nalog.PozivNaBroj);

            if (nalog.PozivNaBroj.Length > 23)
            {
                nalazi.Add(new NalazProvere
                {
                    Tezina = TezinaNalaza.Greska,
                    BrojRadnika = nalog.BrojRadnika,
                    Radnik = nalog.PrimalacNaziv,
                    Provera = "Poziv na broj predugačak",
                    Opis = $"Poziv na broj ima {nalog.PozivNaBroj.Length} znakova, a u fajl staje 23."
                });
            }
        }

        Upisi(red, 210, 6, nalog.DatumValute.ToString("ddMMyy", CultureInfo.InvariantCulture));
        Upisi(red, 216, 1, "0");   // tip dokumenta: nalog za prenos
        Upisi(red, 217, 1, "1");   // tip stavke: individualna
        Upisi(red, 218, 1, "0");   // obično plaćanje

        return red.ToString() + "\r\n";
    }

    /// <summary>
    /// Upisuje vrednost na tačnu poziciju (1-bazirano, kako je u specifikaciji) i seče je
    /// na dozvoljenu dužinu — duža vrednost bi pomerila sva polja iza sebe i obesmislila red.
    /// </summary>
    private static void Upisi(StringBuilder red, int pocetak, int duzina, string? vrednost)
    {
        string tekst = (vrednost ?? "").Replace("\r", " ").Replace("\n", " ");
        if (tekst.Length > duzina) tekst = tekst[..duzina];

        for (int i = 0; i < duzina; i++)
            red[pocetak - 1 + i] = i < tekst.Length ? tekst[i] : ' ';
    }

    /// <summary>
    /// Iznos u parama, desno poravnat i dopunjen nulama, bez decimalnog razdvajača:
    /// „1234,56" se upisuje kao „0000000123456".
    /// </summary>
    private static string Pare(decimal iznos, int duzina)
        => ((long)Math.Round(iznos * 100m, MidpointRounding.AwayFromZero))
            .ToString(CultureInfo.InvariantCulture)
            .PadLeft(duzina, '0');

    /// <summary>Trocifrena šifra plaćanja se deli na oblik (prva cifra) i šifru (preostale dve).</summary>
    private static (string Oblik, string Sifra) RastaviSifruPlacanja(string? sifraPlacanja)
    {
        string s = new((sifraPlacanja ?? "").Where(char.IsDigit).ToArray());

        return s.Length switch
        {
            3 => (s[..1], s[1..]),
            2 => ("2", s),          // data je samo dvocifrena šifra; prenos je podrazumevan oblik
            _ => ("2", "40")        // 240 — isplata zarada
        };
    }

    /// <summary>
    /// Pretvara račun u 18 cifara („fffpppppppppppppkk"): tri cifre banke, trinaest cifara
    /// partije dopunjenih nulama i dve kontrolne. Prihvata i zapis sa crticama i bez njih.
    /// </summary>
    internal static string NormalizujRacun(string? racun, string primalac, string uloga, List<NalazProvere> nalazi)
    {
        string ocisceno = new((racun ?? "").Where(char.IsDigit).ToArray());
        var delovi = (racun ?? "").Split('-', StringSplitOptions.RemoveEmptyEntries);

        string rezultat;
        if (delovi.Length == 3)
        {
            rezultat = delovi[0].PadLeft(3, '0')[..3]
                     + delovi[1].PadLeft(13, '0')
                     + delovi[2].PadLeft(2, '0');
        }
        else
        {
            rezultat = ocisceno.PadLeft(18, '0');
        }

        if (rezultat.Length != 18)
        {
            nalazi.Add(new NalazProvere
            {
                Tezina = TezinaNalaza.Greska,
                Radnik = primalac,
                Provera = $"Neispravan račun {uloga}",
                Opis = $"Račun „{racun}“ se ne može svesti na 18 cifara."
            });
            rezultat = rezultat.Length > 18 ? rezultat[..18] : rezultat.PadLeft(18, '0');
        }

        return rezultat;
    }

    /// <summary>Adresa se čuva kao „Ulica; Mesto"; fajl traži ulicu i mesto u odvojenim poljima.</summary>
    private static string IzdvojUlicu(string adresa)
        => adresa.Split(';')[0].Trim();

    private static string IzdvojMesto(string adresa)
    {
        var delovi = adresa.Split(';');
        return delovi.Length > 1 ? delovi[1].Trim() : "";
    }
}
