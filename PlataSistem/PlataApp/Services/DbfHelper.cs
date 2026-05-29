using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Data.Sqlite;

namespace PlataApp.Services;

public class OpstinaInfo
{
    public string Sifra { get; set; } = "";
    public string Naziv { get; set; } = "";
    public string Prikaz => $"{Naziv} ({Sifra})";
}

public static class DbfHelper
{
    private const string DbfFolder = @"C:\Programi\HOLD\DBF";
    private static readonly string GlobalDbPath = @"C:\PLATA\PlataSistem\plata.db";

    private static void EnsureTablesAndSeed()
    {
        try
        {
            var dbFolder = Path.GetDirectoryName(GlobalDbPath);
            if (!string.IsNullOrEmpty(dbFolder))
            {
                Directory.CreateDirectory(dbFolder);
            }

            using var conn = new SqliteConnection($"Data Source={GlobalDbPath}");
            conn.Open();

            // Create GlobalMesta table if it doesn't exist
            using (var cmd = new SqliteCommand("CREATE TABLE IF NOT EXISTS GlobalMesta (Naziv TEXT PRIMARY KEY);", conn))
            {
                cmd.ExecuteNonQuery();
            }

            // Create GlobalOpstine table if it doesn't exist
            using (var cmd = new SqliteCommand("CREATE TABLE IF NOT EXISTS GlobalOpstine (Sifra TEXT PRIMARY KEY, Naziv TEXT);", conn))
            {
                cmd.ExecuteNonQuery();
            }

            // Check if seeding is needed for Mesta
            bool seedMesta = false;
            using (var cmd = new SqliteCommand("SELECT COUNT(*) FROM GlobalMesta;", conn))
            {
                var count = Convert.ToInt32(cmd.ExecuteScalar());
                if (count == 0) seedMesta = true;
            }

            if (seedMesta)
            {
                var dbfMesta = ParseMestaDbf();
                if (dbfMesta.Count > 0)
                {
                    using var transaction = conn.BeginTransaction();
                    try
                    {
                        foreach (var m in dbfMesta)
                        {
                            using var cmd = new SqliteCommand("INSERT OR IGNORE INTO GlobalMesta (Naziv) VALUES (@naziv);", conn, transaction);
                            cmd.Parameters.AddWithValue("@naziv", m);
                            cmd.ExecuteNonQuery();
                        }
                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                    }
                }
            }

            // Check if seeding is needed for Opstine
            bool seedOpstine = false;
            using (var cmd = new SqliteCommand("SELECT COUNT(*) FROM GlobalOpstine;", conn))
            {
                var count = Convert.ToInt32(cmd.ExecuteScalar());
                if (count == 0) seedOpstine = true;
            }

            if (seedOpstine)
            {
                var dbfOpstine = ParseOpstineDbf();
                if (dbfOpstine.Count > 0)
                {
                    using var transaction = conn.BeginTransaction();
                    try
                    {
                        foreach (var o in dbfOpstine)
                        {
                            using var cmd = new SqliteCommand("INSERT OR IGNORE INTO GlobalOpstine (Sifra, Naziv) VALUES (@sifra, @naziv);", conn, transaction);
                            cmd.Parameters.AddWithValue("@sifra", o.Sifra);
                            cmd.Parameters.AddWithValue("@naziv", o.Naziv);
                            cmd.ExecuteNonQuery();
                        }
                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                    }
                }
            }
        }
        catch
        {
            // Catch silently and let loading fall back to direct DBF read
        }
    }

    public static List<string> LoadMesta()
    {
        EnsureTablesAndSeed();

        var list = new List<string>();
        try
        {
            using var conn = new SqliteConnection($"Data Source={GlobalDbPath}");
            conn.Open();
            using var cmd = new SqliteCommand("SELECT Naziv FROM GlobalMesta ORDER BY Naziv;", conn);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(reader.GetString(0));
            }
        }
        catch
        {
            return ParseMestaDbf();
        }

        if (list.Count == 0)
        {
            return ParseMestaDbf();
        }

        return list;
    }

    public static List<OpstinaInfo> LoadOpstine()
    {
        EnsureTablesAndSeed();

        var list = new List<OpstinaInfo>();
        try
        {
            using var conn = new SqliteConnection($"Data Source={GlobalDbPath}");
            conn.Open();
            using var cmd = new SqliteCommand("SELECT Sifra, Naziv FROM GlobalOpstine ORDER BY Naziv;", conn);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new OpstinaInfo
                {
                    Sifra = reader.GetString(0),
                    Naziv = reader.GetString(1)
                });
            }
        }
        catch
        {
            return ParseOpstineDbf();
        }

        if (list.Count == 0)
        {
            return ParseOpstineDbf();
        }

        return list;
    }

    private static List<OpstinaInfo> ParseOpstineDbf()
    {
        var list = new List<OpstinaInfo>();
        string path = Path.Combine(DbfFolder, "Opstine.dbf");
        if (!File.Exists(path)) return list;

        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new BinaryReader(fs);

            reader.ReadByte(); // version
            reader.ReadBytes(3); // yy, mm, dd
            int numRecords = reader.ReadInt32();
            short headerLength = reader.ReadInt16();
            short recordLength = reader.ReadInt16();

            // Read field lengths
            fs.Position = 32;
            int fieldCount = (headerLength - 33) / 32;
            int[] fieldLengths = new int[fieldCount];
            for (int i = 0; i < fieldCount; i++)
            {
                fs.Position = 32 + i * 32 + 16;
                fieldLengths[i] = reader.ReadByte();
            }

            if (fieldCount < 2) return list;

            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            var enc = Encoding.GetEncoding(852); // OEM Latin 2 for Serbian text

            fs.Position = headerLength;
            for (int r = 0; r < numRecords; r++)
            {
                byte deleteFlag = reader.ReadByte();
                byte[] sifBytes = reader.ReadBytes(fieldLengths[0]);
                byte[] nazBytes = reader.ReadBytes(fieldLengths[1]);

                // Skip remainder of record if there are more than 2 fields
                for (int f = 2; f < fieldCount; f++)
                {
                    reader.ReadBytes(fieldLengths[f]);
                }

                if (deleteFlag == '*') continue; // deleted record

                string sifra = enc.GetString(sifBytes).Trim().PadLeft(3, '0');
                string naziv = enc.GetString(nazBytes).Trim().ToUpper();

                if (!string.IsNullOrWhiteSpace(sifra) && !string.IsNullOrWhiteSpace(naziv))
                {
                    list.Add(new OpstinaInfo { Sifra = sifra, Naziv = naziv });
                }
            }
        }
        catch
        {
            // Fallback empty list or basic items
        }

        // Sort alphabetically by name
        list.Sort((a, b) => string.Compare(a.Naziv, b.Naziv, StringComparison.OrdinalIgnoreCase));
        return list;
    }

    private static List<string> ParseMestaDbf()
    {
        var list = new List<string>();
        string path = Path.Combine(DbfFolder, "Mesta.dbf");
        if (!File.Exists(path)) return list;

        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new BinaryReader(fs);

            reader.ReadByte(); // version
            reader.ReadBytes(3); // yy, mm, dd
            int numRecords = reader.ReadInt32();
            short headerLength = reader.ReadInt16();
            short recordLength = reader.ReadInt16();

            fs.Position = 32;
            int fieldCount = (headerLength - 33) / 32;
            int[] fieldLengths = new int[fieldCount];
            for (int i = 0; i < fieldCount; i++)
            {
                fs.Position = 32 + i * 32 + 16;
                fieldLengths[i] = reader.ReadByte();
            }

            if (fieldCount < 1) return list;

            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            var enc = Encoding.GetEncoding(852);

            fs.Position = headerLength;
            for (int r = 0; r < numRecords; r++)
            {
                byte deleteFlag = reader.ReadByte();
                byte[] nazBytes = reader.ReadBytes(fieldLengths[0]);

                for (int f = 1; f < fieldCount; f++)
                {
                    reader.ReadBytes(fieldLengths[f]);
                }

                if (deleteFlag == '*') continue;

                string naziv = enc.GetString(nazBytes).Trim().ToUpper();
                if (!string.IsNullOrWhiteSpace(naziv) && !list.Contains(naziv))
                {
                    list.Add(naziv);
                }
            }
        }
        catch
        {
            // Fallback
        }

        list.Sort(StringComparer.OrdinalIgnoreCase);
        return list;
    }
}
