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
        
        string dbfDir = @"C:\PLATA\PLATA\KOR28";
        Console.WriteLine("==================================================================");
        Console.WriteLine("        ANALIZA MIGRACIJE I 'RED_BROJ' KOLONE U DBF FAJLOVIMA");
        Console.WriteLine("==================================================================");
        
        // 1. Učitavanje RADNICI.DBF (Aktivni radnici)
        var radniciDbf = Path.Combine(dbfDir, "RADNICI.DBF");
        var radniciNames = new Dictionary<int, string>();
        if (File.Exists(radniciDbf))
        {
            var options = new DbfDataReaderOptions { Encoding = cp852, SkipDeletedRecords = false };
            using var reader = new DbfDataReader.DbfDataReader(radniciDbf, options);
            var cols = Enumerable.Range(0, reader.FieldCount).Select(i => reader.GetName(i).ToUpper().Trim()).ToList();
            int pos = 0;
            while (reader.Read())
            {
                pos++;
                int rb = GetInt(reader, cols, "RED_BROJ", "BR_RADNIK", "SIFRA");
                string name = GetString(reader, cols, "RADNIK", "IME", "IME_I_PRE", "NAZIV");
                int id = rb > 0 ? rb : pos;
                if (!radniciNames.ContainsKey(id))
                {
                    radniciNames[id] = name;
                }
            }
            Console.WriteLine($"[RADNICI.DBF] Učitano {radniciNames.Count} aktivnih radnika.");
        }
        else
        {
            Console.WriteLine("[RADNICI.DBF] NE POSTOJI!");
        }

        // 2. Učitavanje RADNICII.DBF (Istorija radnika)
        var radniciiDbf = Path.Combine(dbfDir, "RADNICII.DBF");
        var radniciiNames = new Dictionary<int, string>();
        var radniciiAllVersions = new Dictionary<int, List<(int period, string name)>>();
        if (File.Exists(radniciiDbf))
        {
            var options = new DbfDataReaderOptions { Encoding = cp852, SkipDeletedRecords = true };
            using var reader = new DbfDataReader.DbfDataReader(radniciiDbf, options);
            var cols = Enumerable.Range(0, reader.FieldCount).Select(i => reader.GetName(i).ToUpper().Trim()).ToList();
            while (reader.Read())
            {
                int rb = GetInt(reader, cols, "RED_BROJ", "BR_RADNIK", "SIFRA");
                string name = GetString(reader, cols, "RADNIK", "IME", "IME_I_PRE", "NAZIV");
                int god = GetInt(reader, cols, "GODINA");
                int mes = GetInt(reader, cols, "MESEC");
                int period = god * 12 + mes;
                
                if (rb > 0 && !string.IsNullOrWhiteSpace(name))
                {
                    if (!radniciiAllVersions.ContainsKey(rb))
                        radniciiAllVersions[rb] = new List<(int, string)>();
                    radniciiAllVersions[rb].Add((period, name));
                }
            }
            
            foreach (var kvp in radniciiAllVersions)
            {
                var latest = kvp.Value.OrderByDescending(x => x.period).First();
                radniciiNames[kvp.Key] = latest.name;
            }
            Console.WriteLine($"[RADNICII.DBF] Učitano {radniciiNames.Count} istorijskih radnika.");
        }
        else
        {
            Console.WriteLine("[RADNICII.DBF] NE POSTOJI!");
        }

        // 3. Analiza RAD_SATI.DBF (Tekući radni sati)
        var radSatiDbf = Path.Combine(dbfDir, "RAD_SATI.DBF");
        AnalyzeTransactions(radSatiDbf, cp852, "RAD_SATI.DBF", radniciNames, radniciiNames);

        // 4. Analiza RADSATII.DBF (Istorijski radni sati)
        var radSatiiDbf = Path.Combine(dbfDir, "RADSATII.DBF");
        AnalyzeTransactions(radSatiiDbf, cp852, "RADSATII.DBF", radniciNames, radniciiNames);

        // 5. Analiza OBRACUN.DBF (Tekući obračuni)
        var obracunDbf = Path.Combine(dbfDir, "OBRACUN.DBF");
        AnalyzeObracun(obracunDbf, cp852, "OBRACUN.DBF", radniciNames, radniciiNames);

        // 6. Analiza OBRACUNI.DBF (Istorijski obračuni)
        var obracuniDbf = Path.Combine(dbfDir, "OBRACUNI.DBF");
        AnalyzeObracun(obracuniDbf, cp852, "OBRACUNI.DBF", radniciNames, radniciiNames);

        Console.WriteLine("\n=== ANALIZA ZAVRŠENA ===");
    }

    static void AnalyzeTransactions(string dbfPath, Encoding enc, string label, 
        Dictionary<int, string> radniciNames, Dictionary<int, string> radniciiNames)
    {
        Console.WriteLine($"\n--- Analiza {label} ---");
        if (!File.Exists(dbfPath))
        {
            Console.WriteLine("Fajl ne postoji!");
            return;
        }

        try
        {
            var options = new DbfDataReaderOptions { Encoding = enc, SkipDeletedRecords = true };
            using var reader = new DbfDataReader.DbfDataReader(dbfPath, options);
            var cols = Enumerable.Range(0, reader.FieldCount).Select(i => reader.GetName(i).ToUpper().Trim()).ToList();
            
            int totalRows = 0;
            int matchedActive = 0;
            int matchedHistory = 0;
            int orphaned = 0;
            int nameMismatches = 0;
            
            var mismatchExamples = new List<string>();
            var orphanedExamples = new HashSet<int>();
            
            while (reader.Read())
            {
                totalRows++;
                int rb = GetInt(reader, cols, "RED_BROJ");
                string nameInSati = GetString(reader, cols, "RADNIK");
                
                string officialName = null;
                bool isHistory = false;
                if (radniciNames.TryGetValue(rb, out var activeName))
                {
                    officialName = activeName;
                    matchedActive++;
                }
                else if (radniciiNames.TryGetValue(rb, out var histName))
                {
                    officialName = histName;
                    matchedHistory++;
                    isHistory = true;
                }
                else
                {
                    orphaned++;
                    orphanedExamples.Add(rb);
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(nameInSati) && !string.IsNullOrWhiteSpace(officialName))
                {
                    // Ukloni suvišne razmake i uporedi
                    string n1 = nameInSati.Replace(" ", "").ToLower();
                    string n2 = officialName.Replace(" ", "").ToLower();
                    if (n1 != n2)
                    {
                        nameMismatches++;
                        if (mismatchExamples.Count < 5)
                        {
                            mismatchExamples.Add($"RBR {rb}: Ime u {label}='{nameInSati}' vs Zvanično='{officialName}' {(isHistory ? "(Istorija)" : "(Aktivni)")}");
                        }
                    }
                }
            }

            Console.WriteLine($"  Ukupno redova: {totalRows}");
            Console.WriteLine($"  Poklapanje sa aktivnim radnicima (RADNICI.DBF): {matchedActive}");
            Console.WriteLine($"  Poklapanje sa istorijskim radnicima (RADNICII.DBF): {matchedHistory}");
            Console.WriteLine($"  Siročad (nepostojeći RBR u šifarniku): {orphaned}");
            if (orphaned > 0)
            {
                Console.WriteLine($"    Primeri siročadi (RBR): {string.Join(", ", orphanedExamples.OrderBy(x => x).Take(15))}");
            }
            Console.WriteLine($"  Neslaganja u imenima za isti RED_BROJ: {nameMismatches}");
            if (nameMismatches > 0)
            {
                Console.WriteLine("    Primeri neslaganja:");
                foreach (var ex in mismatchExamples)
                {
                    Console.WriteLine($"      - {ex}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Greška: {ex.Message}");
        }
    }

    static void AnalyzeObracun(string dbfPath, Encoding enc, string label, 
        Dictionary<int, string> radniciNames, Dictionary<int, string> radniciiNames)
    {
        Console.WriteLine($"\n--- Analiza {label} ---");
        if (!File.Exists(dbfPath))
        {
            Console.WriteLine("Fajl ne postoji!");
            return;
        }

        try
        {
            var options = new DbfDataReaderOptions { Encoding = enc, SkipDeletedRecords = true };
            using var reader = new DbfDataReader.DbfDataReader(dbfPath, options);
            var cols = Enumerable.Range(0, reader.FieldCount).Select(i => reader.GetName(i).ToUpper().Trim()).ToList();
            
            int totalRows = 0;
            int matchedActive = 0;
            int matchedHistory = 0;
            int orphaned = 0;
            
            var orphanedExamples = new HashSet<int>();
            
            while (reader.Read())
            {
                totalRows++;
                int rb = GetInt(reader, cols, "RED_BROJ");
                
                if (radniciNames.ContainsKey(rb))
                {
                    matchedActive++;
                }
                else if (radniciiNames.ContainsKey(rb))
                {
                    matchedHistory++;
                }
                else
                {
                    orphaned++;
                    orphanedExamples.Add(rb);
                }
            }

            Console.WriteLine($"  Ukupno redova: {totalRows}");
            Console.WriteLine($"  Poklapanje sa aktivnim radnicima (RADNICI.DBF): {matchedActive}");
            Console.WriteLine($"  Poklapanje sa istorijskim radnicima (RADNICII.DBF): {matchedHistory}");
            Console.WriteLine($"  Siročad (nepostojeći RBR u šifarniku): {orphaned}");
            if (orphaned > 0)
            {
                Console.WriteLine($"    Primeri siročadi (RBR): {string.Join(", ", orphanedExamples.OrderBy(x => x).Take(15))}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Greška: {ex.Message}");
        }
    }

    static string GetString(DbfDataReader.DbfDataReader r, List<string> cols, params string[] names)
    {
        foreach (var n in names) { int i = cols.IndexOf(n); if (i >= 0) try { return r.GetString(i).Trim(); } catch { } }
        return "";
    }

    static int GetInt(DbfDataReader.DbfDataReader r, List<string> cols, params string[] names)
    {
        foreach (var n in names)
        {
            int i = cols.IndexOf(n);
            if (i >= 0) try { return Convert.ToInt32(r.GetValue(i)); } catch { }
        }
        return 0;
    }
}
