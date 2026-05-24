using System;
using System.IO;
using System.Text;
using System.Linq;
using DbfDataReader;
using Microsoft.EntityFrameworkCore;
using PlataData;
using PlataData.Models;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
var cp852 = Encoding.GetEncoding(852);

Console.WriteLine("=== DIAGNOSTIC: KORISNIC.DBF ===");
string dbfPath = @"c:\PLATA\PLATA\KORISNIC.DBF";
if (File.Exists(dbfPath))
{
    try
    {
        var options = new DbfDataReaderOptions { Encoding = cp852, SkipDeletedRecords = true };
        using var reader = new DbfDataReader.DbfDataReader(dbfPath, options);
        var columns = Enumerable.Range(0, reader.FieldCount).Select(i => reader.GetName(i).ToUpper().Trim()).ToList();
        Console.WriteLine($"Kolone u KORISNIC.DBF: {string.Join(", ", columns)}");
        
        int rCnt = 0;
        while (reader.Read())
        {
            rCnt++;
            Console.WriteLine($"\nZapis #{rCnt}:");
            for (int i = 0; i < reader.FieldCount; i++)
            {
                Console.WriteLine($"  {reader.GetName(i),-15} : '{reader.GetValue(i)?.ToString()?.Trim()}'");
            }
        }
        Console.WriteLine($"Ukupno zapisa u KORISNIC.DBF: {rCnt}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Greška pri čitanju KORISNIC.DBF: {ex.Message}");
    }
}
else
{
    Console.WriteLine("KORISNIC.DBF ne postoji!");
}

Console.WriteLine("\n=== DIAGNOSTIC: SQLite c:\\PLATA\\PlataSistem\\plata.db ===");
CheckSqlite(@"c:\PLATA\PlataSistem\plata.db");

Console.WriteLine("\n=== DIAGNOSTIC: SQLite bin\\Debug\\net8.0-windows\\plata.db ===");
string binDb = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"c:\PLATA\PlataSistem\PlataApp\bin\Debug\net8.0-windows\plata.db");
if (File.Exists(binDb))
{
    CheckSqlite(binDb);
}
else
{
    // Proveri relativno
    string localBin = Path.Combine(@"c:\PLATA\PlataSistem\PlataApp\bin\Debug\net8.0-windows", "plata.db");
    if (File.Exists(localBin))
    {
        CheckSqlite(localBin);
    }
    else
    {
        Console.WriteLine("bin\\Debug\\net8.0-windows\\plata.db ne postoji!");
    }
}

void CheckSqlite(string path)
{
    try
    {
        using var db = PlataDbContext.Create(path);
        var firme = db.Firme.ToList();
        Console.WriteLine($"Putanja: {path}");
        Console.WriteLine($"Broj zapisa u tabeli Firme: {firme.Count}");
        foreach (var f in firme)
        {
            Console.WriteLine($"  Id: {f.Id}");
            Console.WriteLine($"  Naziv: '{f.Naziv}'");
            Console.WriteLine($"  Adresa: '{f.Adresa}'");
            Console.WriteLine($"  Grad: '{f.Grad}'");
            Console.WriteLine($"  Pib: '{f.Pib}'");
            Console.WriteLine($"  Mb: '{f.Mb}'");
            Console.WriteLine($"  BankovniRacun: '{f.BankovniRacun}'");
            Console.WriteLine($"  SifraPlacanja: '{f.SifraPlacanja}'");
            Console.WriteLine($"  Telefon: '{f.Telefon}'");
            Console.WriteLine($"  Email: '{f.Email}'");
            Console.WriteLine($"  Napomena: '{f.Napomena}'");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Greška pri čitanju SQLite: {ex.Message}");
    }
}
