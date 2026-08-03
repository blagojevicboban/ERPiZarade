using System.Xml.Linq;
using ERPiZaradeApp.Services;
using ERPiZaradeData.Models;

namespace ERPiZaradeApp.Tests;

/// <summary>
/// Izmenjena PPP-PD prijava (Faza 2.7) i struktura koju specifikacija Poreske uprave
/// izričito propisuje: <b>neispunjen opcioni tag se izostavlja u celosti</b> — prazan
/// <c>&lt;tag/&gt;</c> nije dozvoljen. Prijava sa praznim tagom prolazi generisanje a
/// pada tek kod PU, pa se ovde fiksira.
///
/// Izvor: „Opis XML struktura koje se koriste u procesu podnošenja PPPPD", verzija 3.3,
/// pozicije 1.5, 1.5a, 1.6 i 1.6a Obrasca PPP-PD.
/// </summary>
public class PppPdIzmenaTests
{
    private static readonly XNamespace Tns = "http://pid.purs.gov.rs";
    private static readonly DateTime DatumPlacanja = new(2026, 4, 5);

    private static List<ObracunPlate> Obracuni(string olaksicaOznaka = "")
    {
        var radnik = new Radnik
        {
            Id = 1,
            BrojRadnika = 1,
            ImeIPrezime = "Petrović Petar",
            Jmbg = "0101990710016",
            SifraOpstine = "013",
            Radno_Mesto = "101101000"
        };

        return
        [
            new ObracunPlate
            {
                Id = 1,
                RadnikId = 1,
                Radnik = radnik,
                Godina = 2026,
                Mesec = 3,
                BrutoZarada = 80000m,
                NetoIsplata = 50000m,
                PorezNaDohodak = 5000m,
                DoprinosPioRadnik = 11200m,
                DoprinosPioPoslodavac = 8000m,
                RedovniSati = 176,
                OlaksicaOznaka = olaksicaOznaka,
                OlaksicaPorez = 3500m
            }
        ];
    }

    private static XElement Generisi(IzmenaPrijave? izmena = null,
        IReadOnlyDictionary<string, IReadOnlyList<OlaksicaMfp>>? mfp = null,
        List<ObracunPlate>? obracuni = null)
    {
        string xml = new XmlExportService().GeneratePppPdXml(
            obracuni ?? Obracuni(),
            DatumPlacanja,
            pibFirme: "100000001",
            maticniBrojFirme: "12345678",
            nazivFirme: "Test doo",
            sedisteFirme: "013",
            telefonFirme: "011000000",
            adresaFirme: "Ulica 1",
            emailFirme: "test@test.rs",
            mfpPoOlaksici: mfp,
            izmena: izmena);

        return XDocument.Parse(xml).Root!;
    }

    private static XElement PodaciOPrijavi(XElement koren) => koren.Element(Tns + "PodaciOPrijavi")!;

    [Fact]
    public void RedovnaPrijava_NemaNijedanElementIzmene()
    {
        var prijava = PodaciOPrijavi(Generisi());

        Assert.Null(prijava.Element(Tns + "VrstaIzmene"));
        Assert.Null(prijava.Element(Tns + "JIPD"));
        Assert.Null(prijava.Element(Tns + "BrojResenja"));
        Assert.Null(prijava.Element(Tns + "Osnov"));
    }

    [Fact]
    public void IzmenjenaPrijava_NosiVrstuIzmeneIJipdPrijaveKojaSeMenja()
    {
        var prijava = PodaciOPrijavi(Generisi(new IzmenaPrijave
        {
            VrstaIzmene = VrstaIzmenePrijave.Izmena,
            Jipd = "1234567890123456789"
        }));

        Assert.Equal("1", prijava.Element(Tns + "VrstaIzmene")!.Value);
        Assert.Equal("1234567890123456789", prijava.Element(Tns + "JIPD")!.Value);
    }

    /// <summary>
    /// Redosled elemenata je deo XSD sekvence, pa element na pogrešnom mestu obara prijavu
    /// jednako kao i element koji nedostaje.
    /// </summary>
    [Fact]
    public void IzmenjenaPrijava_ElementiIduPoRedosleduIzSpecifikacije()
    {
        var prijava = PodaciOPrijavi(Generisi(new IzmenaPrijave
        {
            VrstaIzmene = VrstaIzmenePrijave.PoNalazuKontrole,
            Jipd = "42",
            BrojResenja = "47-00123/2026",
            Osnov = OsnovIzmenePrijave.ZalbaPrviStepen
        }));

        var imena = prijava.Elements().Select(e => e.Name.LocalName).ToList();

        Assert.Equal(
            ["KlijentskaOznakaDeklaracije", "VrstaPrijave", "ObracunskiPeriod", "OznakaZaKonacnu",
             "DatumPlacanja", "VrstaIzmene", "JIPD", "BrojResenja", "Osnov", "NajnizaOsnovica"],
            imena);
    }

    [Fact]
    public void IzmenjenaPrijava_PraznoResenjeIOsnovSeIzostavljaju()
    {
        var prijava = PodaciOPrijavi(Generisi(new IzmenaPrijave
        {
            VrstaIzmene = VrstaIzmenePrijave.Izmena,
            Jipd = "42"
        }));

        Assert.Null(prijava.Element(Tns + "BrojResenja"));
        Assert.Null(prijava.Element(Tns + "Osnov"));
    }

    [Fact]
    public void IzmenjenaPrijava_BezJipda_SeOdbija()
    {
        var greska = Assert.Throws<ArgumentException>(() => Generisi(new IzmenaPrijave
        {
            VrstaIzmene = VrstaIzmenePrijave.Izmena,
            Jipd = "   "
        }));

        Assert.Contains("JIPD", greska.Message);
    }

    [Theory]
    [InlineData("12345678901234567890")]  // 20 cifara — preko dozvoljenih 19
    [InlineData("97-123456")]             // crtica
    [InlineData("BOP123")]                // slova
    public void IzmenjenaPrijava_NeispravanJipd_SeOdbija(string jipd)
    {
        Assert.Throws<ArgumentException>(() => Generisi(new IzmenaPrijave
        {
            VrstaIzmene = VrstaIzmenePrijave.Izmena,
            Jipd = jipd
        }));
    }

    [Fact]
    public void BezOlaksice_DeklarisaniMfpSeUopsteNeEmituje()
    {
        var prihod = Generisi()
            .Element(Tns + "DeklarisaniPrihodi")!
            .Element(Tns + "PodaciOPrihodima")!;

        Assert.Null(prihod.Element(Tns + "DeklarisaniMFP"));
    }

    [Fact]
    public void SaOlaksicom_DeklarisaniMfpNosiBarJednoPolje()
    {
        var mfp = new Dictionary<string, IReadOnlyList<OlaksicaMfp>>
        {
            ["24"] = [new OlaksicaMfp { Oznaka = "MFP.1", Izvor = IzvorMfp.UmanjenjePoreza }]
        };

        var prihod = Generisi(mfp: mfp, obracuni: Obracuni("24"))
            .Element(Tns + "DeklarisaniPrihodi")!
            .Element(Tns + "PodaciOPrihodima")!;

        var deklarisani = prihod.Element(Tns + "DeklarisaniMFP");
        Assert.NotNull(deklarisani);

        var polje = deklarisani!.Element(Tns + "MFP")!;
        Assert.Equal("MFP.1", polje.Element(Tns + "Oznaka")!.Value);
        Assert.Equal("3500.00", polje.Element(Tns + "Vrednost")!.Value);
    }

    /// <summary>
    /// Olakšica koja u šifarniku nema nijednu MFP deklaraciju ne sme da proizvede prazan
    /// tag — to je isti nedozvoljeni oblik kao i kod obračuna bez olakšice.
    /// </summary>
    [Fact]
    public void OlaksicaBezMfpDeklaracija_NeProizvodiPrazanTag()
    {
        var mfp = new Dictionary<string, IReadOnlyList<OlaksicaMfp>>
        {
            ["24"] = []
        };

        var prihod = Generisi(mfp: mfp, obracuni: Obracuni("24"))
            .Element(Tns + "DeklarisaniPrihodi")!
            .Element(Tns + "PodaciOPrihodima")!;

        Assert.Null(prihod.Element(Tns + "DeklarisaniMFP"));
    }
}
