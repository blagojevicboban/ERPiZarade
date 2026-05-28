using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using PlataData.Models;

namespace PlataApp.Services;

public class XmlExportService
{
    private static readonly XNamespace tns = "http://pid.purs.gov.rs";
    private static readonly XNamespace xsi = "http://www.w3.org/2001/XMLSchema-instance";

    public string GeneratePppPdXml(
        List<ObracunPlate> obracuni, 
        DateTime datumPlacanja, 
        string pibFirme, 
        string maticniBrojFirme, 
        string nazivFirme,
        string sedisteFirme = "079",
        string telefonFirme = "010-123456",
        string adresaFirme = "Ulica i broj",
        string emailFirme = "info@firma.rs",
        string? klijentskaOznaka = null,
        string vrstaPrijave = "1",
        string oznakaZaKonacnu = "K",
        string najnizaOsnovica = "0",
        string tipIsplatioca = "1")
    {
        if (obracuni == null || obracuni.Count == 0)
            throw new ArgumentException("Lista obračuna ne može biti prazna.");

        var prvi = obracuni.First();
        int godina = prvi.Godina;
        int mesec = prvi.Mesec;
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

                    // Šifra vrste prihoda (SVP)
                    // Standard code is 101101000 for regular, or 109101000 for sick leave paid by employer
                    // If Radno_Mesto is a 9-digit code, use it. Otherwise, use fallbacks based on sick leave.
                    string svp = "101101000";
                    bool jePenzioner = !string.IsNullOrWhiteSpace(obracun.Radnik.Radno_Mesto) && 
                                       obracun.Radnik.Radno_Mesto.TrimStart().StartsWith("109");

                    if (obracun.BrutoBolovanje > obracun.BrutoZarada)
                    {
                        svp = "109101000";
                    }
                    else if (!string.IsNullOrWhiteSpace(obracun.Radnik.Radno_Mesto) && 
                             obracun.Radnik.Radno_Mesto.Length == 9 && 
                             obracun.Radnik.Radno_Mesto.All(char.IsDigit))
                    {
                        svp = obracun.Radnik.Radno_Mesto;
                    }
                    else if (jePenzioner)
                    {
                        svp = "101109000"; // SVP za zaposlene penzionere
                    }

                    int efektivniSati = obracun.RedovniSati + obracun.PrekovremeneSati;
                    int fondSati = obracun.RedovniSati + obracun.BolovanjeSati + obracun.PrekovremeneSati + obracun.GodisnjioOdmorSati;

                    return new XElement(tns + "PodaciOPrihodima",
                        new XElement(tns + "RedniBroj", (index + 1).ToString()),
                        new XElement(tns + "VrstaIdentifikatoraPrimaoca", "1"),
                        new XElement(tns + "IdentifikatorPrimaoca", obracun.Radnik.Jmbg),
                        new XElement(tns + "Prezime", prezime),
                        new XElement(tns + "Ime", ime),
                        new XElement(tns + "OznakaPrebivalista", sedisteFirme),
                        new XElement(tns + "SVP", svp),
                        new XElement(tns + "BrojKalendarskihDana", "30"),
                        new XElement(tns + "BrojEfektivnihSati", efektivniSati.ToString()),
                        new XElement(tns + "MesecniFondSati", fondSati.ToString()),
                        new XElement(tns + "Bruto", bruto.ToString("F2")),
                        new XElement(tns + "OsnovicaPorez", poreskaOsnovica.ToString("F2")),
                        new XElement(tns + "Porez", porez.ToString("F2")),
                        new XElement(tns + "OsnovicaDoprinosi", osnovicaDoprinosa.ToString("F2")),
                        new XElement(tns + "PIO", totalPio.ToString("F2")),
                        new XElement(tns + "ZDR", totalZdr.ToString("F2")),
                        new XElement(tns + "NEZ", totalNez.ToString("F2")),
                        new XElement(tns + "PIOBen", "0.00"),
                        new XElement(tns + "DeklarisaniMFP")
                    );
                })
            )
        );

        var doc = new XDocument(new XDeclaration("1.0", "UTF-8", null), root);
        using var sw = new StringWriter();
        doc.Save(sw);
        return sw.ToString();
    }
}
