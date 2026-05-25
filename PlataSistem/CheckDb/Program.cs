using System;
using System.IO;
using Microsoft.Data.Sqlite;

var dbPath = args.Length > 0 ? args[0] : @"C:\PLATA\PlataSistem\plata.db";
Console.WriteLine($"Proverujem bazu: {dbPath}");

using var conn = new SqliteConnection($"Data Source={dbPath}");
conn.Open();

// Tabele
var cmd = conn.CreateCommand();
cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' ORDER BY name";
var reader = cmd.ExecuteReader();
Console.WriteLine("\nTabele u bazi:");
while (reader.Read())
    Console.WriteLine($"  - {reader.GetString(0)}");
reader.Close();

// Provera Firme tabele i podataka
try
{
    cmd.CommandText = "SELECT COUNT(*) FROM Firme";
    var count = (long)(cmd.ExecuteScalar() ?? 0L);
    Console.WriteLine($"\nBroj zapisa u Firme: {count}");
    
    if (count > 0)
    {
        cmd.CommandText = "SELECT * FROM Firme LIMIT 1";
        reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            Console.WriteLine("\nPodaci iz Firme:");
            for (int i = 0; i < reader.FieldCount; i++)
                Console.WriteLine($"  {reader.GetName(i)}: '{reader.GetValue(i)}'");
        }
        reader.Close();
    }
}
catch (Exception ex)
{
    Console.WriteLine($"\n[GREŠKA] Tabela Firme ne postoji ili greška: {ex.Message}");
}
