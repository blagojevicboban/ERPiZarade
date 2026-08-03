using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using ERPiZaradeData.Models;
using System.Globalization;
namespace ERPiZaradeApp.Services;

/// <summary>
/// Podaci kojima se prijava izjašnjava da menja ranije podnetu — pozicije 1.5–1.6a Obrasca
/// PPP-PD. Odvojeni su od ostalih parametara zato što idu zajedno ili nikako: bez
/// <see cref="Jipd"/> Poreska uprava ne zna koju prijavu menjamo.
/// </summary>
public sealed class IzmenaPrijave
{
    public VrstaIzmenePrijave VrstaIzmene { get; init; } = VrstaIzmenePrijave.Izmena;

    /// <summary>JIPD prijave koja se menja. Najviše 19 cifara, bez razmaka i crtica.</summary>
    public string Jipd { get; init; } = "";

    /// <summary>Broj rešenja kontrole ili suda; prazno kad izmenu podnosi sam obveznik.</summary>
    public string BrojResenja { get; init; } = "";

    public OsnovIzmenePrijave Osnov { get; init; } = OsnovIzmenePrijave.Nema;
}

public class XmlExportService
{
    private static readonly XNamespace tns = "http://pid.purs.gov.rs";
    private static readonly XNamespace xsi = "http://www.w3.org/2001/XMLSchema-instance";

    private sealed class Utf8StringWriter : StringWriter
    {
        public override Encoding Encoding => Encoding.UTF8;
    }

    /// <summary>
    /// Elementi izmene prijave, po redosledu iz XSD-a: <c>VrstaIzmene</c> (1.5),
    /// <c>JIPD</c> (1.5a), <c>BrojResenja</c> (1.6), <c>Osnov</c> (1.6a).
    ///
    /// Specifikacija izričito zabranjuje prazne tagove („opcije &lt;tag&gt;&lt;/tag&gt; ili
    /// &lt;tag/&gt; nisu dozvoljene"), pa se neispunjena polja izostavljaju u celosti,
    /// a za redovnu prijavu se ne emituje nijedno.
    /// </summary>
    private static IEnumerable<XElement> ElementiIzmene(IzmenaPrijave? izmena)
    {
        if (izmena == null || izmena.VrstaIzmene == VrstaIzmenePrijave.Nema)
            yield break;

        yield return new XElement(tns + "VrstaIzmene", ((int)izmena.VrstaIzmene).ToString(CultureInfo.InvariantCulture));
        yield return new XElement(tns + "JIPD", izmena.Jipd.Trim());

        if (!string.IsNullOrWhiteSpace(izmena.BrojResenja))
            yield return new XElement(tns + "BrojResenja", izmena.BrojResenja.Trim());

        if (izmena.Osnov != OsnovIzmenePrijave.Nema)
            yield return new XElement(tns + "Osnov", ((int)izmena.Osnov).ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Odbija izmenu koja bi prošla generisanje a pala kod Poreske uprave. JIPD je po
    /// specifikaciji obavezan čim je popunjena vrsta izmene, i to kao broj do 19 cifara.
    /// </summary>
    private static void ProveriIzmenu(IzmenaPrijave? izmena)
    {
        if (izmena == null || izmena.VrstaIzmene == VrstaIzmenePrijave.Nema) return;

        string jipd = izmena.Jipd?.Trim() ?? "";

        if (jipd.Length == 0)
            throw new ArgumentException(
                "Izmenjena prijava mora da nosi JIPD prijave koja se menja (polje 1.5a). " +
                "JIPD dodeljuje Poreska uprava pri prijemu prvobitne prijave.",
                nameof(izmena));

        if (jipd.Length > 19 || !jipd.All(char.IsDigit))
            throw new ArgumentException(
                $"JIPD „{jipd}\" nije ispravan — očekuje se do 19 cifara, bez razmaka i crtica.",
                nameof(izmena));
    }

    /// <summary>
    /// Multifunkcionalno polje kroz koje se prijavljuje poreska olakšica.
    ///
    /// Struktura je po specifikaciji Poreske uprave: <c>MFP</c> se ponavlja onoliko puta
    /// koliko je polja popunjeno, sa oznakom <c>MFP.1</c>–<c>MFP.12</c> i vrednošću u kojoj je
    /// decimalni razdvajač <b>tačka</b>. Šta koje polje znači zavisi od SVP šifre, pa se
    /// mapiranje uzima iz šifarnika olakšica, a ne iz koda.
    ///
    /// Bez olakšice se element ne emituje.
    /// </summary>
    private static XElement? DeklarisaniMfp(
        ObracunPlate obracun,
        decimal osnovicaPoreza,
        decimal osnovicaDoprinosa,
        IReadOnlyDictionary<string, IReadOnlyList<OlaksicaMfp>>? mfpPoOlaksici)
    {
        // Bez olakšice se tag ne emituje uopšte. Prazan <DeklarisaniMFP/> je do sada išao u
        // svaku prijavu, a specifikacija ga ne dozvoljava: opcioni tag koji se ne koristi
        // isključuje se iz strukture, a kad se navede mora da nosi bar jedan MFP.
        if (mfpPoOlaksici == null
            || string.IsNullOrWhiteSpace(obracun.OlaksicaOznaka)
            || !mfpPoOlaksici.TryGetValue(obracun.OlaksicaOznaka, out var deklaracije)
            || deklaracije.Count == 0)
        {
            return null;
        }

        var element = new XElement(tns + "DeklarisaniMFP");

        foreach (var deklaracija in deklaracije)
        {
            decimal vrednost = deklaracija.Izvor switch
            {
                IzvorMfp.UmanjenjePoreza => obracun.OlaksicaPorez,
                IzvorMfp.UmanjenjeDoprinosa => obracun.OlaksicaDoprinosi,
                IzvorMfp.OsnovicaPoreza => osnovicaPoreza,
                IzvorMfp.OsnovicaDoprinosa => osnovicaDoprinosa,
                IzvorMfp.FiksnaVrednost => deklaracija.FiksnaVrednost,
                _ => 0m
            };

            element.Add(new XElement(tns + "MFP",
                new XElement(tns + "Oznaka", deklaracija.Oznaka),
                new XElement(tns + "Vrednost", vrednost.ToString("F2", CultureInfo.InvariantCulture))));
        }

        return element;
    }

    /// <param name="sedisteFirme">
    /// Šifra opštine sedišta iz kartona firme. Namerno nema podrazumevanu vrednost —
    /// ranije je stajala literalna „079", koja je tiho davala pogrešno zaglavlje svakoj
    /// firmi koja nije iz te opštine.
    /// </param>
    public string GeneratePppPdXml(
        List<ObracunPlate> obracuni,
        DateTime datumPlacanja,
        string pibFirme,
        string maticniBrojFirme,
        string nazivFirme,
        string sedisteFirme,
        string telefonFirme,
        string adresaFirme,
        string emailFirme,
        string? klijentskaOznaka = null,
        string vrstaPrijave = "1",
        string oznakaZaKonacnu = "K",
        string najnizaOsnovica = "0",
        string tipIsplatioca = "1",
        int? brojKalendarskihDana = null,
        IReadOnlyDictionary<string, IReadOnlyList<OlaksicaMfp>>? mfpPoOlaksici = null,
        IzmenaPrijave? izmena = null)
    {
        if (obracuni == null || obracuni.Count == 0)
            throw new ArgumentException("Lista obračuna ne može biti prazna.");

        ProveriIzmenu(izmena);

        // Prijava sa praznim ili izmišljenim sedištem prolazi generisanje, a pada tek kod
        // Poreske uprave — zato se odbija ovde, dok je ispravka još jeftina.
        if (string.IsNullOrWhiteSpace(sedisteFirme))
            throw new ArgumentException(
                "Šifra opštine sedišta nije uneta. Popunite je u kartonu firme pre generisanja PPP-PD prijave.",
                nameof(sedisteFirme));

        var prvi = obracuni.First();
        int godina = prvi.Godina;
        int mesec = prvi.Mesec;
        int danaUMesecu = brojKalendarskihDana ?? DateTime.DaysInMonth(godina, mesec);
        string obracunskiPeriod = $"{godina}-{mesec:D2}";
        string finalKlijentskaOznaka = string.IsNullOrWhiteSpace(klijentskaOznaka) 
            ? $"DECL-{datumPlacanja:dd.MM.yyyy}" 
            : klijentskaOznaka;

        // Filter out employees without JMBG or valid details
        var validObracuni = obracuni.Where(o => o.Radnik != null && !string.IsNullOrWhiteSpace(o.Radnik.Jmbg)).ToList();

        var root = new XElement(tns + "PodaciPoreskeDeklaracije",
            new XAttribute(XNamespace.Xmlns + "xsi", xsi.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "tns", tns.NamespaceName),
            new XAttribute(xsi + "schemaLocation", "http://pid.purs.gov.rs"),

            new XElement(tns + "PodaciOPrijavi",
                new XElement(tns + "KlijentskaOznakaDeklaracije", finalKlijentskaOznaka),
                new XElement(tns + "VrstaPrijave", vrstaPrijave),
                new XElement(tns + "ObracunskiPeriod", obracunskiPeriod),
                new XElement(tns + "OznakaZaKonacnu", oznakaZaKonacnu),
                new XElement(tns + "DatumPlacanja", datumPlacanja.ToString("yyyy-MM-dd")),
                ElementiIzmene(izmena),
                new XElement(tns + "NajnizaOsnovica", najnizaOsnovica)
            ),

            new XElement(tns + "PodaciOIsplatiocu",
                new XElement(tns + "TipIsplatioca", tipIsplatioca),
                new XElement(tns + "PoreskiIdentifikacioniBroj", pibFirme),
                new XElement(tns + "BrojZaposlenih", validObracuni.Count.ToString()),
                new XElement(tns + "MaticniBrojisplatioca", maticniBrojFirme),
                new XElement(tns + "NazivPrezimeIme", nazivFirme.ToUpper()),
                new XElement(tns + "SedistePrebivaliste", sedisteFirme),
                new XElement(tns + "Telefon", telefonFirme),
                new XElement(tns + "UlicaIBroj", adresaFirme),
                new XElement(tns + "eMail", emailFirme)
            ),

            new XElement(tns + "DeklarisaniPrihodi",
                validObracuni.Select((obracun, index) =>
                {
                    // Split first name and last name
                    string prezime = "";
                    string ime = "";
                    var parts = obracun.Radnik.ImeIPrezime.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length > 0) prezime = parts[0];
                    if (parts.Length > 1) ime = parts[1];
                    else ime = "[Bez imena]";

                    decimal bruto = obracun.BrutoZarada + obracun.BrutoBolovanje;
                    decimal poreskaOsnovica = obracun.PoreskaOsnovica;
                    decimal porez = obracun.PorezNaDohodak;

                    // Combined social security contribution bases
                    decimal pioRadnik = obracun.DoprinosPioRadnik;
                    decimal zdrRadnik = obracun.DoprinosZdravstvoRadnik;
                    decimal nezRadnik = obracun.DoprinosNezaposlenostRadnik;

                    decimal pioPoslodavac = obracun.DoprinosPioPoslodavac;
                    decimal zdrPoslodavac = obracun.DoprinosZdravstvoPoslodavac;
                    decimal nezPoslodavac = obracun.DoprinosNezaposlenostPoslodavac;

                    // Standard social security bases sum (employee + employer parts)
                    decimal totalPio = pioRadnik + pioPoslodavac;
                    decimal totalZdr = zdrRadnik + zdrPoslodavac;
                    decimal totalNez = nezRadnik + nezPoslodavac;

                    // Basic verification of contribution base
                    decimal osnovicaDoprinosa = bruto;
                    // Standard minimum base rule: total PIO rate is 24% (14% employee + 10% employer)
                    if (totalPio > 0 && bruto > 0)
                    {
                        osnovicaDoprinosa = Math.Round(totalPio / 0.24m, 2);
                    }

                    // Šifra vrste prihoda (SVP) — jedna logika za prijavu, ekran i godišnju potvrdu.
                    string svp = SvpService.Odredi(obracun);

                    int efektivniSati = obracun.UkupnoSati;
                    int fondSati = obracun.UkupnoSati;

                    return new XElement(tns + "PodaciOPrihodima",
                        new XElement(tns + "RedniBroj", (index + 1).ToString()),
                        new XElement(tns + "VrstaIdentifikatoraPrimaoca", "1"),
                        new XElement(tns + "IdentifikatorPrimaoca", obracun.Radnik.Jmbg),
                        new XElement(tns + "Prezime", prezime),
                        new XElement(tns + "Ime", ime),
                        new XElement(tns + "OznakaPrebivalista", 
                            !string.IsNullOrWhiteSpace(obracun.Radnik.SifraOpstine) 
                                ? obracun.Radnik.SifraOpstine 
                                : sedisteFirme),
                        new XElement(tns + "SVP", svp),
                        new XElement(tns + "BrojKalendarskihDana", danaUMesecu.ToString()),
                        new XElement(tns + "BrojEfektivnihSati", efektivniSati.ToString()),
                        new XElement(tns + "MesecniFondSati", fondSati.ToString()),
                        new XElement(tns + "Bruto", bruto.ToString("F2", CultureInfo.InvariantCulture)),
                        new XElement(tns + "OsnovicaPorez", poreskaOsnovica.ToString("F2", CultureInfo.InvariantCulture)),
                        new XElement(tns + "Porez", porez.ToString("F2", CultureInfo.InvariantCulture)),
                        new XElement(tns + "OsnovicaDoprinosi", osnovicaDoprinosa.ToString("F2", CultureInfo.InvariantCulture)),
                        new XElement(tns + "PIO", totalPio.ToString("F2", CultureInfo.InvariantCulture)),
                        new XElement(tns + "ZDR", totalZdr.ToString("F2", CultureInfo.InvariantCulture)),
                        new XElement(tns + "NEZ", totalNez.ToString("F2", CultureInfo.InvariantCulture)),
                        new XElement(tns + "PIOBen", "0.00"),
                        DeklarisaniMfp(obracun, poreskaOsnovica, osnovicaDoprinosa, mfpPoOlaksici)
                    );
                })
            )
        );

        var doc = new XDocument(new XDeclaration("1.0", "UTF-8", null), root);
        using var sw = new Utf8StringWriter();
        doc.Save(sw);
        return sw.ToString();
    }
}
