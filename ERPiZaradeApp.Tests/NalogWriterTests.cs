using System.Text;
using System.Text.Json;
using ERPiZaradeApp.Services;

namespace ERPiZaradeApp.Tests;

/// <summary>
/// Halcom TXT je format fiksnih pozicija — pomereno polje za jedan znak pomera i sva
/// polja iza njega, a fajl banka odbija bez korisnog objašnjenja. Testovi zato proveravaju
/// tačne pozicije i dužine iz specifikacije, ne samo da izlaz „liči".
/// </summary>
public class HalcomPpzWriterTests
{
    private static readonly DateTime Valuta = new(2026, 4, 5);

    private static NalogZaPrenos Zarada(decimal iznos = 1234.56m) => new()
    {
        Vrsta = VrstaNaloga.NetoZarada,
        PlatilacNaziv = "TEST DOO",
        PlatilacRacun = "160-0000000000-11",
        PrimalacNaziv = "Petar Petrović",
        PrimalacRacun = "265-1234567890-45",
        PrimalacAdresa = "Kneza Miloša 12; Beograd",
        Iznos = iznos,
        SifraPlacanja = "240",
        Svrha = "Isplata zarade za 03/2026",
        DatumValute = Valuta,
        BrojRadnika = 1
    };

    private static NalogZaPrenos Porezi() => new()
    {
        Vrsta = VrstaNaloga.ObjedinjenaNaplata,
        PlatilacNaziv = "TEST DOO",
        PlatilacRacun = "160-0000000000-11",
        PrimalacNaziv = "Objedinjena naplata",
        PrimalacRacun = "840-4848-37",
        PrimalacAdresa = "Poreska uprava; Beograd",
        Iznos = 15000m,
        SifraPlacanja = "254",
        ModelPozivaNaBroj = "97",
        PozivNaBroj = "9712345678901234A",
        Svrha = "Porezi i doprinosi 03/2026",
        DatumValute = Valuta
    };

    private static List<string> Redovi(params NalogZaPrenos[] nalozi)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        byte[] bajtovi = HalcomPpzWriter.Generisi(nalozi, out _);
        return Encoding.GetEncoding(1250).GetString(bajtovi)
            .Split("\r\n", StringSplitOptions.RemoveEmptyEntries)
            .ToList();
    }

    /// <summary>Čita polje po specifikaciji: pozicija je 1-bazirana.</summary>
    private static string Polje(string red, int pocetak, int duzina)
        => red.Substring(pocetak - 1, duzina);

    [Fact]
    public void Generisi_DajeAdresnuSabirnuIPoJednuIndividualnuStavku()
    {
        var redovi = Redovi(Zarada(), Porezi());

        Assert.Equal(4, redovi.Count);
        Assert.Equal("0", Polje(redovi[0], 180, 1));   // adresna
        Assert.Equal("9", Polje(redovi[1], 180, 1));   // sabirna
        Assert.Equal("1", Polje(redovi[2], 217, 1));   // individualna
        Assert.Equal("1", Polje(redovi[3], 217, 1));
    }

    [Fact]
    public void Generisi_IndividualnaStavkaImaTacnoPropisanuDuzinu()
    {
        var redovi = Redovi(Zarada());

        Assert.Equal(180, redovi[0].Length);
        Assert.Equal(180, redovi[1].Length);
        Assert.Equal(218, redovi[2].Length);
    }

    /// <summary>„1234,56" se upisuje kao „0000000123456" — u parama, bez zareza, sa vodećim nulama.</summary>
    [Fact]
    public void Generisi_IznosSeUpisujeUParamaSaVodecimNulama()
    {
        var redovi = Redovi(Zarada(1234.56m));

        Assert.Equal("0000000123456", Polje(redovi[2], 172, 13));
    }

    [Fact]
    public void Generisi_SabirnaStavkaNosiZbirIBrojNaloga()
    {
        var redovi = Redovi(Zarada(1000m), Zarada(500.50m));

        Assert.Equal("000000000150050", Polje(redovi[1], 64, 15));
        Assert.Equal("00002", Polje(redovi[1], 79, 5));
    }

    /// <summary>
    /// Trocifrena šifra plaćanja se u fajl upisuje razdvojeno: prva cifra je oblik plaćanja
    /// na poziciji 167, preostale dve su šifra na poziciji 168. Upis „240" u dvocifreno
    /// polje bi pomerio sve iza njega.
    /// </summary>
    [Fact]
    public void Generisi_SifraPlacanja240_SeDeliNaOblik2ISifru40()
    {
        var redovi = Redovi(Zarada());

        Assert.Equal("2", Polje(redovi[2], 167, 1));
        Assert.Equal("40", Polje(redovi[2], 168, 2));
    }

    [Fact]
    public void Generisi_RacunSeSvodiNa18Cifara()
    {
        var redovi = Redovi(Zarada());

        // „265-1234567890-45" → 265 (banka) + 0001234567890 (partija) + 45 (kontrolni broj)
        Assert.Equal("265000123456789045", Polje(redovi[2], 1, 18));
    }

    [Fact]
    public void Generisi_ZaradaNemaPozivNaBroj_APoreziGaImaju()
    {
        var redovi = Redovi(Zarada(), Porezi());

        Assert.Equal(new string(' ', 23), Polje(redovi[2], 187, 23));
        Assert.Equal("97", Polje(redovi[3], 185, 2));
        Assert.StartsWith("9712345678901234A", Polje(redovi[3], 187, 23));
    }

    [Fact]
    public void Generisi_DatumValuteJeUFormatuDdMMyy()
    {
        var redovi = Redovi(Zarada());

        Assert.Equal("050426", Polje(redovi[2], 210, 6));
    }

    /// <summary>Predugačko ime ne sme da pomeri polja iza sebe.</summary>
    [Fact]
    public void Generisi_PredugackoImePrimaoca_SeSeceANeSiriRed()
    {
        var nalog = Zarada();
        var dugacak = new NalogZaPrenos
        {
            PlatilacNaziv = nalog.PlatilacNaziv,
            PlatilacRacun = nalog.PlatilacRacun,
            PrimalacNaziv = new string('X', 80),
            PrimalacRacun = nalog.PrimalacRacun,
            PrimalacAdresa = nalog.PrimalacAdresa,
            Iznos = nalog.Iznos,
            SifraPlacanja = nalog.SifraPlacanja,
            Svrha = nalog.Svrha,
            DatumValute = nalog.DatumValute
        };

        var redovi = Redovi(dugacak);

        Assert.Equal(218, redovi[2].Length);
        Assert.Equal(new string('X', 35), Polje(redovi[2], 19, 35));
    }

    [Fact]
    public void Generisi_AdresaSeDeliNaUlicuIMesto()
    {
        var redovi = Redovi(Zarada());

        Assert.Equal("Kneza Miloša 12", Polje(redovi[2], 54, 35).TrimEnd());
        Assert.Equal("Beograd", Polje(redovi[2], 89, 10).TrimEnd());
    }
}

/// <summary>
/// Trezorski ePP (Uprava za trezor) prima JSON, ne XML. Nazivi polja su deo ugovora sa
/// sistemom — preimenovanje bilo kog od njih obara učitavanje celog fajla.
/// </summary>
public class TrezorEppWriterTests
{
    private static readonly DateTime Valuta = new(2026, 4, 5);

    private static NalogZaPrenos Zarada() => new()
    {
        Vrsta = VrstaNaloga.NetoZarada,
        PlatilacNaziv = "TEST DOO",
        PlatilacRacun = "840-1992-69",
        PrimalacNaziv = "Petar Petrović",
        PrimalacRacun = "265-1234567890-45",
        PrimalacAdresa = "Kneza Miloša 12; Beograd",
        Iznos = 1234.56m,
        SifraPlacanja = "240",
        Svrha = "Isplata zarade za 03/2026",
        DatumValute = Valuta,
        BrojRadnika = 1
    };

    private static JsonElement PrviNalog(params NalogZaPrenos[] nalozi)
    {
        string json = TrezorEppWriter.Generisi(nalozi, out _);
        return JsonDocument.Parse(json).RootElement[0];
    }

    [Fact]
    public void Generisi_KoristiNazivePoljaKojeEppOcekuje()
    {
        var nalog = PrviNalog(Zarada());

        Assert.Equal("Isplata zarade za 03/2026", nalog.GetProperty("PaymentBasis").GetString());
        Assert.Equal(240, nalog.GetProperty("PaymentCode").GetInt32());
        Assert.Equal(1234.56m, nalog.GetProperty("Amount").GetDecimal());
        Assert.Equal("840-1992-69", nalog.GetProperty("DebtorBankAccount").GetString());
        Assert.Equal("Petar Petrović", nalog.GetProperty("CreditorName").GetString());
        Assert.Equal("265-1234567890-45", nalog.GetProperty("CreditorBankAccount").GetString());
    }

    /// <summary>Bez poziva na broj polja se izostavljaju, a ne šalju kao prazan string.</summary>
    [Fact]
    public void Generisi_ZaradaBezPozivaNaBroj_IzostavljaPoljaPoziva()
    {
        var nalog = PrviNalog(Zarada());

        Assert.False(nalog.TryGetProperty("CreditorCode", out _));
        Assert.False(nalog.TryGetProperty("CreditorCodeModel", out _));
    }

    [Fact]
    public void Generisi_DijakritikaOstajeCitljiva()
    {
        string json = TrezorEppWriter.Generisi([Zarada()], out _);

        Assert.Contains("Petar Petrović", json);
        Assert.DoesNotContain("\\u", json);
    }

    [Fact]
    public void Generisi_PrekoracenjeBrojaNaloga_JePrijavljeno()
    {
        var mnogo = Enumerable.Range(0, TrezorEppWriter.MaxNalogaPoFajlu + 1).Select(_ => Zarada()).ToList();

        TrezorEppWriter.Generisi(mnogo, out var nalazi);

        Assert.Contains(nalazi, n => n.Provera == "Previše naloga u fajlu");
    }

    [Fact]
    public void Generisi_PrazanaAdresaPrimaoca_JeGreska()
    {
        var nalog = Zarada();
        var bezAdrese = new NalogZaPrenos
        {
            PlatilacNaziv = nalog.PlatilacNaziv,
            PlatilacRacun = nalog.PlatilacRacun,
            PrimalacNaziv = nalog.PrimalacNaziv,
            PrimalacRacun = nalog.PrimalacRacun,
            PrimalacAdresa = "",
            Iznos = nalog.Iznos,
            SifraPlacanja = nalog.SifraPlacanja,
            Svrha = nalog.Svrha,
            DatumValute = nalog.DatumValute
        };

        TrezorEppWriter.Generisi([bezAdrese], out var nalazi);

        Assert.Contains(nalazi, n => n.Provera == "Nedostaje adresa primaoca");
    }
}
