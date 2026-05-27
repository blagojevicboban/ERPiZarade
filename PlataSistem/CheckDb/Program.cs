using System;
using Microsoft.Data.Sqlite;

var dbPath = @"C:\PLATA\PlataSistem\plata.db";
Console.WriteLine($"Proverujem bazu: {dbPath}");

using var conn = new SqliteConnection($"Data Source={dbPath}");
conn.Open();

void PrintTableSchema(string tableName)
{
    Console.WriteLine($"\nKolone za tabelu {tableName}:");
    using var cmd = conn.CreateCommand();
    cmd.CommandText = $"PRAGMA table_info({tableName})";
    using var reader = cmd.ExecuteReader();
    while (reader.Read())
    {
        Console.WriteLine($"  - {reader.GetString(1)} ({reader.GetString(2)})");
    }
}

PrintTableSchema("RadniSati");
PrintTableSchema("ObracuniPlata");
