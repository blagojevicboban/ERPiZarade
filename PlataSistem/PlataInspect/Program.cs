using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using DbfDataReader;

namespace PlataInspect;

class Program
{
    static void Main(string[] args)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var cp852 = Encoding.GetEncoding(852);
        
        string dbPath = @"C:\PlataApp\plata.db";
        Console.WriteLine($"=== VERIFIKACIJA SVIH PODATAKA IZ SQLITE BAZE: {dbPath} ===");
        
        if (!File.Exists(dbPath))
        {
            Console.WriteLine($"Baza ne postoji na putanji: {dbPath}");
            return;
        }

        using var db = PlataData.PlataDbContext.Create(dbPath);
        
        // 1. Nadji radnika Srdjan Vidanovic i Divna Veselinovic
        var radnici = db.Radnici.ToList();
        var srdjan = radnici.FirstOrDefault(r => r.ImeIPrezime.Contains("Vidanovi") || r.ImeIPrezime.Contains("Srđan"));
        var divna = radnici.FirstOrDefault(r => r.ImeIPrezime.Contains("Veselinovi") || r.ImeIPrezime.Contains("Divna"));

        if (srdjan != null)
        {
            Console.WriteLine($"\n[OK] Pronađen Srđan: ID={srdjan.Id}, BrojRadnika={srdjan.BrojRadnika}, Ime={srdjan.ImeIPrezime}");
            var obracuniSrdjan = db.ObracuniPlata
                .Where(o => o.RadnikId == srdjan.Id)
                .OrderByDescending(o => o.Godina).ThenByDescending(o => o.Mesec)
                .ToList();
            
            Console.WriteLine($"Pronađeno {obracuniSrdjan.Count} obračuna za Srđana. Zadnjih 3:");
            foreach (var o in obracuniSrdjan.Take(3))
            {
                decimal totalBruto = o.BrutoZarada + o.BrutoBolovanje;
                decimal neto1 = totalBruto - o.PorezNaDohodak - (o.DoprinosPioRadnik + o.DoprinosZdravstvoRadnik + o.DoprinosNezaposlenostRadnik);
                Console.WriteLine($"  Period {o.Mesec:D2}/{o.Godina}: BrutoZarada={o.BrutoZarada:N2}, BrutoBolovanje={o.BrutoBolovanje:N2}, BrutoNaknade={o.BrutoNaknade:N2}, TotalBruto={totalBruto:N2}, Neto1={neto1:N2}, ZaIsplatu={o.NetoIsplata:N2}, Kredit={o.KreditObustava:N2}, Samodoprinosi={o.Samodoprinosi:N2}");
            }
        }
        else
        {
            Console.WriteLine("[!] Upozorenje: Srđan Vidanović nije pronađen po imenu u bazi.");
        }

        if (divna != null)
        {
            Console.WriteLine($"\n[OK] Pronađena Divna: ID={divna.Id}, BrojRadnika={divna.BrojRadnika}, Ime={divna.ImeIPrezime}");
            
            // Nadji sve obracune sa obustavama
            var obracuniDivnaSaObustavama = db.ObracuniPlata
                .Where(o => o.RadnikId == divna.Id && (o.Samodoprinosi > 0 || o.KreditObustava > 0))
                .OrderByDescending(o => o.Godina).ThenByDescending(o => o.Mesec)
                .ToList();
            
            Console.WriteLine($"Pronađeno {obracuniDivnaSaObustavama.Count} obračuna sa obustavama za Divnu. Top 5:");
            foreach (var o in obracuniDivnaSaObustavama.Take(5))
            {
                decimal totalBruto = o.BrutoZarada + o.BrutoBolovanje;
                Console.WriteLine($"  Period {o.Mesec:D2}/{o.Godina}: BrutoZarada={o.BrutoZarada:N2}, TotalBruto={totalBruto:N2}, ZaIsplatu={o.NetoIsplata:N2}, Kredit={o.KreditObustava:N2}, Samodoprinosi={o.Samodoprinosi:N2}");
                
                // Detaljne obustave/samodoprinosi za ovaj period
                var det = db.Samodoprinosi
                    .Where(s => s.RadnikId == divna.Id && s.Godina == o.Godina && s.Mesec == o.Mesec)
                    .ToList();
                if (det.Any())
                {
                    Console.WriteLine("    Detaljne stavke obustava:");
                    foreach (var s in det)
                    {
                        Console.WriteLine($"      - Opis: {s.Opis,-20} | Iznos: {s.Iznos:N2} RSD");
                    }
                }
            }
        }
        else
        {
            Console.WriteLine("[!] Upozorenje: Divna Veselinović nije pronađena po imenu u bazi.");
        }

        // 3. Ukupne statistike baze
        Console.WriteLine("\n=== STATISTIKA SQLITE BAZE ===");
        Console.WriteLine($"Ukupno radnika u bazi:  {db.Radnici.Count()}");
        Console.WriteLine($"Ukupno obračuna u bazi: {db.ObracuniPlata.Count()}");
        Console.WriteLine($"Ukupno detaljnih obustava (Samodoprinosi): {db.Samodoprinosi.Count()}");
        
        var topSamodop = db.Samodoprinosi
            .Select(s => new { s.Opis, s.Iznos })
            .AsEnumerable()
            .GroupBy(s => s.Opis)
            .Select(g => new { Opis = g.Key, Count = g.Count(), Sum = g.Sum(x => x.Iznos) })
            .OrderByDescending(x => x.Sum)
            .Take(10)
            .ToList();
        
        Console.WriteLine("\nTop 10 detaljnih vrsta obustava po ukupnom iznosu:");
        foreach (var ts in topSamodop)
        {
            Console.WriteLine($"  Stavka: {ts.Opis,-20} | Broj zapisa: {ts.Count,-5} | Ukupan Iznos: {ts.Sum:N2} RSD");
        }
    }

    static void QuerySqlite(string dbPath)
    {
        if (!File.Exists(dbPath))
        {
            Console.WriteLine($"Database not found at: {dbPath}");
            return;
        }

        try
        {
            using var db = PlataData.PlataDbContext.Create(dbPath);
            var o = db.ObracuniPlata
                .FirstOrDefault(x => x.RadnikId == 10 && x.Godina == 2013 && x.Mesec == 5);

            if (o != null)
            {
                Console.WriteLine($"  05/2013: BrutoZarada={o.BrutoZarada:N2}, NetoIsplata={o.NetoIsplata:N2}, Kredit={o.KreditObustava:N2}, Samodoprinosi={o.Samodoprinosi:N2}");
            }
            else
            {
                Console.WriteLine("  No record found for 05/2013.");
            }

            var samodopDetails = db.Samodoprinosi
                .Where(s => s.RadnikId == 10)
                .OrderByDescending(s => s.Godina).ThenByDescending(s => s.Mesec)
                .ToList();

            Console.WriteLine($"Found {samodopDetails.Count} detailed samodoprinosi records in SQLite for worker 10:");
            foreach (var s in samodopDetails.Take(10))
            {
                Console.WriteLine($"  {s.Mesec:D2}/{s.Godina}: Opis={s.Opis}, Iznos={s.Iznos:N2}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error reading SQLite: {ex.Message}");
        }
    }




    static void PrintDbfSchema(string dbfPath, string label)
    {
        Console.WriteLine($"\n=== SCHEMA FOR {label} ===");
        if (!File.Exists(dbfPath))
        {
            Console.WriteLine("File does not exist!");
            return;
        }

        try
        {
            var options = new DbfDataReaderOptions { Encoding = Encoding.GetEncoding(852), SkipDeletedRecords = true };
            using var reader = new DbfDataReader.DbfDataReader(dbfPath, options);
            for (int i = 0; i < reader.FieldCount; i++)
            {
                Console.WriteLine($"  Col #{i}: Name={reader.GetName(i)}, Type={reader.GetFieldType(i)?.Name}, DataTypeName={reader.GetDataTypeName(i)}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }

    static int GetColIndex(DbfDataReader.DbfDataReader r, string name)
    {
        for (int i = 0; i < r.FieldCount; i++)
        {
            if (r.GetName(i).ToUpper().Trim() == name.ToUpper()) return i;
        }
        return -1;
    }

    static void PrintDbfRecords(string dbfPath, Encoding enc, string label, Func<DbfDataReader.DbfDataReader, bool> filter)
    {
        Console.WriteLine($"\n--- File: {label} ---");
        if (!File.Exists(dbfPath))
        {
            Console.WriteLine("File does not exist!");
            return;
        }

        try
        {
            var options = new DbfDataReaderOptions { Encoding = enc, SkipDeletedRecords = true };
            using var reader = new DbfDataReader.DbfDataReader(dbfPath, options);
            var columns = Enumerable.Range(0, reader.FieldCount)
                                    .Select(i => reader.GetName(i).ToUpper().Trim())
                                     .ToList();

            int matchedCount = 0;
            while (reader.Read())
            {
                if (filter(reader))
                {
                    matchedCount++;
                    Console.WriteLine($"Record #{matchedCount}:");
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        string colName = reader.GetName(i);
                        object val = reader.GetValue(i);
                        if (val != null && !val.ToString().Trim().Equals("0") && !val.ToString().Trim().Equals("0.00") && !val.ToString().Trim().Equals(""))
                        {
                            Console.WriteLine($"  {colName,-12} : {val.ToString().Trim()}");
                        }
                    }
                }
            }
            if (matchedCount == 0)
            {
                Console.WriteLine("No records matched the filter.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error reading {label}: {ex.Message}");
        }
    }
}
