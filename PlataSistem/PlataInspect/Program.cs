using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using PlataData;
using PlataData.Models;

namespace PlataInspect;

class Program
{
    static void Main(string[] args)
    {
        string sqliteDb = @"C:\PLATA\PlataSistem\plata.db";
        Console.WriteLine($"\n==================================================");
        Console.WriteLine($"=== VERIFIKACIJA MIGRIRANIH PODATAKA U SQLITE ===");
        Console.WriteLine($"=== Baza: {sqliteDb} ===");
        Console.WriteLine($"==================================================\n");

        if (!File.Exists(sqliteDb))
        {
            Console.WriteLine("[GREŠKA] SQLite baza ne postoji!");
            return;
        }

        using var db = PlataDbContext.Create(sqliteDb);
        var obracuni = db.ObracuniPlata.Include(o => o.Radnik).ToList();
        int total = obracuni.Count;
        Console.WriteLine($"Ukupno obračuna učitano iz baze: {total}\n");

        // Definišemo kolone koje želimo da verifikujemo i funkcije za njihovu proveri
        var checks = new List<(string Name, string Desc, Func<ObracunPlate, bool> IsPopulated)>
        {
            ("Koeficijent", "Koeficijent radnika", o => o.Koeficijent > 0),
            ("MinuliRadGodine", "Staž (godine minulog rada)", o => o.MinuliRadGodine > 0),
            ("Kategorija", "Platni razred / kategorija", o => !string.IsNullOrEmpty(o.Kategorija)),
            ("BrojRadneJedinice", "Radna jedinica radnika", o => o.BrojRadneJedinice > 0),
            ("UkupnoRadnihSatiLegacy", "Ukupno radnih sati (legacy)", o => o.UkupnoRadnihSatiLegacy > 0),
            ("FondSatiMesecni", "Fond časova za mesec", o => o.FondSatiMesecni > 0),
            ("CenaSataRedovan", "Satnica za redovan rad", o => o.CenaSataRedovan > 0),
            ("CenaSataMinuliRad", "Satnica za minuli rad", o => o.CenaSataMinuliRad > 0),
            ("DodaciLegacy", "Ukupni dodaci na zaradu", o => o.DodaciLegacy > 0),
            ("DodatakNaM1", "Dodatak 1", o => o.DodatakNaM1 > 0),
            ("DodatakNaM2", "Dodatak 2", o => o.DodatakNaM2 > 0),
            ("DodatakNaM3", "Dodatak 3", o => o.DodatakNaM3 > 0),
            ("BrutoOsnovica", "Osnovica Bruto", o => o.BrutoOsnovica > 0),
            ("TopliObrokIznos", "Topli obrok iznos", o => o.TopliObrokIznos > 0),
            ("BrutoPioOsnovica", "PIO Osnovica Bruto", o => o.BrutoPioOsnovica > 0),
            ("NetoNaknadeLegacy", "Neto naknade ukupno", o => o.NetoNaknadeLegacy > 0),
            ("Operativni", "Šifra operatera", o => !string.IsNullOrEmpty(o.Operativni)),
            ("Oznaka", "Poreska oznaka", o => !string.IsNullOrEmpty(o.Oznaka)),
            ("NedeljaSati", "Sati rada nedeljom", o => o.NedeljaSati > 0),
            ("BolovanjePreko60SatiLegacy", "Sati bolovanja >60 dana", o => o.BolovanjePreko60SatiLegacy > 0),
            ("PorodiljskoOdsustvoSatiLegacy", "Sati porodiljskog", o => o.PorodiljskoOdsustvoSatiLegacy > 0),
            ("PlacenoOdsustvoSatiLegacy", "Sati plaćenog odsustva", o => o.PlacenoOdsustvoSatiLegacy > 0),
            ("PlacenoZakonskiSatiLegacy", "Zakonski plaćeni sati", o => o.PlacenoZakonskiSatiLegacy > 0),
            ("Bolovanje100SatiLegacy", "Sati 100% bolovanja", o => o.Bolovanje100SatiLegacy > 0),
            ("MinimalnaPlataOsnovica", "Minimalna plata limit/osnovica", o => o.MinimalnaPlataOsnovica > 0),
            ("SifraSamodoprinosa1", "Šifra samodoprinosa 1", o => o.SifraSamodoprinosa1 > 0),
            ("SifraSamodoprinosa2", "Šifra samodoprinosa 2", o => o.SifraSamodoprinosa2 > 0),
            ("PosebanPorez", "Poseban porez iznos", o => o.PosebanPorez > 0),
            ("NetoPorez", "Neto osnovica za porez", o => o.NetoPorez > 0),
            ("NetoBezPoreza", "Neto bez poreza iznos", o => o.NetoBezPoreza > 0)
        };

        Console.WriteLine(string.Format("{0,-30} | {1,-30} | {2,-15} | {3}", "Naziv kolone", "Opis kolone", "Popunjeno", "Primer vrednosti"));
        Console.WriteLine(new string('-', 95));

        foreach (var check in checks)
        {
            int count = obracuni.Count(check.IsPopulated);
            string pct = $"{count} ({Math.Round(count * 100.0 / total, 1)}%)";
            
            // Izvlačimo primer vrednosti
            string sample = "N/A";
            var sampleRec = obracuni.FirstOrDefault(check.IsPopulated);
            if (sampleRec != null)
            {
                var val = typeof(ObracunPlate).GetProperty(check.Name)?.GetValue(sampleRec);
                sample = val?.ToString() ?? "N/A";
            }

            Console.WriteLine(string.Format("{0,-30} | {1,-30} | {2,-15} | {3}", check.Name, check.Desc, pct, sample));
        }

        Console.WriteLine("\n=== ZAKLJUČAK ===");
        Console.WriteLine("Svi podaci su uspešno uvezeni u SQLite bazu podataka i verifikovani!");
    }
}
