using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using DbfDataReader;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
var cp852 = Encoding.GetEncoding(852);

string dbPath = @"C:\PlataApp\Baze\firma_100188310_PSSS_PIROT_DOO_PIROT.db";
string dbfPath = @"C:\PLATA\PLATA\KOR28\RADNICII.DBF";

using var connection = new SqliteConnection($"Data Source={dbPath}");
connection.Open();

var opts = new DbfDataReaderOptions { Encoding = cp852, SkipDeletedRecords = true };
using var reader = new DbfDataReader.DbfDataReader(dbfPath, opts);
var cols = Enumerable.Range(0, reader.FieldCount).Select(i => reader.GetName(i).ToUpper().Trim()).ToList();

int godIdx = cols.IndexOf("GODINA");
int mesIdx = cols.IndexOf("MESEC");
int redBrojIdx = cols.IndexOf("RED_BROJ");
if (redBrojIdx < 0) redBrojIdx = cols.IndexOf("BR_RADNIK");

if (godIdx < 0 || mesIdx < 0 || redBrojIdx < 0) {
    Console.WriteLine("Nedostaju ključne kolone u DBF-u.");
    return;
}

// Map old Radnici (Godina=0, Mesec=0)
var idToBrojRadnika = new Dictionary<int, int>();
using (var cmd = new SqliteCommand("SELECT Id, BrojRadnika FROM Radnici WHERE Godina = 0 AND Mesec = 0", connection))
using (var r = cmd.ExecuteReader()) {
    while (r.Read()) {
        idToBrojRadnika[r.GetInt32(0)] = r.GetInt32(1);
    }
}

// Cache new Radnici (Godina > 0)
var newRadniciDict = new Dictionary<string, int>(); 
using (var cmd = new SqliteCommand("SELECT Id, BrojRadnika, Godina, Mesec FROM Radnici WHERE Godina > 0", connection))
using (var r = cmd.ExecuteReader()) {
    while (r.Read()) {
        newRadniciDict[$"{r.GetInt32(1)}-{r.GetInt32(2)}-{r.GetInt32(3)}"] = r.GetInt32(0);
    }
}

// Cache all ObracuniPlata
var obracuni = new List<(int Id, int RadnikId, int Godina, int Mesec)>();
using (var cmd = new SqliteCommand("SELECT Id, RadnikId, Godina, Mesec FROM ObracuniPlata", connection))
using (var r = cmd.ExecuteReader()) {
    while (r.Read()) {
        obracuni.Add((r.GetInt32(0), r.GetInt32(1), r.GetInt32(2), r.GetInt32(3)));
    }
}

Console.WriteLine($"Ukupno obračuna: {obracuni.Count}");
Console.WriteLine("Učitavanje iz DBF-a i ažuriranje baze...");

int insertedCount = 0;
int updatedObracuniCount = 0;

using var transaction = connection.BeginTransaction();

while (reader.Read())
{
    try {
        int god = Convert.ToInt32(reader.GetValue(godIdx));
        int mes = Convert.ToInt32(reader.GetValue(mesIdx));
        int brRadnika = Convert.ToInt32(reader.GetValue(redBrojIdx));

        if (god <= 0 || mes <= 0 || mes > 12 || brRadnika <= 0) continue;

        string key = $"{brRadnika}-{god}-{mes}";
        int radnikId;

        if (newRadniciDict.TryGetValue(key, out var existingId)) {
            radnikId = existingId;
        } else {
            string GetString(params string[] names) {
                foreach (var n in names) {
                    int i = cols.IndexOf(n);
                    if (i >= 0) try { return reader.GetValue(i)?.ToString() ?? ""; } catch {}
                }
                return "";
            }
            decimal GetDecimal(params string[] names) {
                foreach (var n in names) {
                    int i = cols.IndexOf(n);
                    if (i >= 0) try { return Convert.ToDecimal(reader.GetValue(i)); } catch {}
                }
                return 0m;
            }
            int GetInt(params string[] names) {
                foreach (var n in names) {
                    int i = cols.IndexOf(n);
                    if (i >= 0) try { return Convert.ToInt32(reader.GetValue(i)); } catch {}
                }
                return 0;
            }
            string GetIntAsString(params string[] names) {
                return GetInt(names).ToString();
            }

            string ImeIPrezime = GetString("RADNIK", "IME", "IME_I_PRE", "NAZIV");
            string MaticniBroj = GetString("MAT_BROJ", "MAT_BR");
            string jj = GetString("JMBG");
            string Jmbg = !string.IsNullOrWhiteSpace(jj) ? jj.Trim() : (MaticniBroj.Trim().Length == 13 ? MaticniBroj.Trim() : "");
            decimal Koeficijent = GetDecimal("KOEFIC", "KOEFICIJE", "KOEF");
            string Kategorija = GetIntAsString("RAZRED", "KAT", "KATEGORIJ");
            int BrojRadneJedinice = GetInt("RAD_JED");
            string NazivBanke = GetIntAsString("BANKA");
            string BankovniRacun = GetString("BROJ_TR", "ZIRO", "RACUN");
            string Radno_Mesto = GetString("RADNO_M", "RADNO_MES");
            string SifraOpstine = GetString("OZNAKA", "OZNAKA_OP", "OPSTINA");
            int Aktivan = GetString("AKTIVAN").ToUpper() == "DA" ? 1 : 0;
            decimal OsnovnaPlata = GetDecimal("MIN_PLATA", "OSNOVA", "OSN_PLATA");

            if (string.IsNullOrWhiteSpace(ImeIPrezime)) {
                ImeIPrezime = $"Radnik #{brRadnika}";
            }

            using (var insertCmd = new SqliteCommand(@"
                INSERT INTO Radnici (
                    BrojRadnika, Godina, Mesec, ImeIPrezime, MaticniBroj, Jmbg, Koeficijent,
                    Kategorija, BrojRadneJedinice, NazivBanke, BankovniRacun, Radno_Mesto,
                    SifraOpstine, Aktivan, OsnovnaPlata, DatumUnosa, DatumRodjenja, MestoRodjenja, 
                    AdresaStanovanja, Mesto, DatumZaposlenja, DatumPrestanka, StopaPio, StopaZdravstvo, 
                    StopaNezaposlenost, LicnoOslobodjenje, DatumIzmene
                ) VALUES (
                    @BrojRadnika, @Godina, @Mesec, @ImeIPrezime, @MaticniBroj, @Jmbg, @Koeficijent,
                    @Kategorija, @BrojRadneJedinice, @NazivBanke, @BankovniRacun, @Radno_Mesto,
                    @SifraOpstine, @Aktivan, @OsnovnaPlata, @DatumUnosa, '', '', '', '', '', '', 0, 0, 0, 0, ''
                );
                SELECT last_insert_rowid();", connection, transaction))
            {
                insertCmd.Parameters.AddWithValue("@BrojRadnika", brRadnika);
                insertCmd.Parameters.AddWithValue("@Godina", god);
                insertCmd.Parameters.AddWithValue("@Mesec", mes);
                insertCmd.Parameters.AddWithValue("@ImeIPrezime", ImeIPrezime);
                insertCmd.Parameters.AddWithValue("@MaticniBroj", MaticniBroj);
                insertCmd.Parameters.AddWithValue("@Jmbg", Jmbg);
                insertCmd.Parameters.AddWithValue("@Koeficijent", Koeficijent);
                insertCmd.Parameters.AddWithValue("@Kategorija", Kategorija);
                insertCmd.Parameters.AddWithValue("@BrojRadneJedinice", BrojRadneJedinice);
                insertCmd.Parameters.AddWithValue("@NazivBanke", NazivBanke);
                insertCmd.Parameters.AddWithValue("@BankovniRacun", BankovniRacun);
                insertCmd.Parameters.AddWithValue("@Radno_Mesto", Radno_Mesto);
                insertCmd.Parameters.AddWithValue("@SifraOpstine", SifraOpstine);
                insertCmd.Parameters.AddWithValue("@Aktivan", Aktivan);
                insertCmd.Parameters.AddWithValue("@OsnovnaPlata", OsnovnaPlata);
                insertCmd.Parameters.AddWithValue("@DatumUnosa", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

                radnikId = Convert.ToInt32(insertCmd.ExecuteScalar());
            }
            
            newRadniciDict[key] = radnikId;
            insertedCount++;
        }

        var obracun = obracuni.FirstOrDefault(o => o.Godina == god && o.Mesec == mes && 
                                                   idToBrojRadnika.ContainsKey(o.RadnikId) && 
                                                   idToBrojRadnika[o.RadnikId] == brRadnika);
        
        if (obracun.Id != 0 && obracun.RadnikId != radnikId) {
            int stariRadnikId = obracun.RadnikId;
            
            using (var u1 = new SqliteCommand("UPDATE ObracuniPlata SET RadnikId = @nid WHERE Id = @id", connection, transaction)) {
                u1.Parameters.AddWithValue("@nid", radnikId);
                u1.Parameters.AddWithValue("@id", obracun.Id);
                u1.ExecuteNonQuery();
            }
            using (var u2 = new SqliteCommand("UPDATE RadniSati SET RadnikId = @nid WHERE RadnikId = @sid AND Godina = @g AND Mesec = @m", connection, transaction)) {
                u2.Parameters.AddWithValue("@nid", radnikId);
                u2.Parameters.AddWithValue("@sid", stariRadnikId);
                u2.Parameters.AddWithValue("@g", god);
                u2.Parameters.AddWithValue("@m", mes);
                u2.ExecuteNonQuery();
            }
            using (var u3 = new SqliteCommand("UPDATE DoprinosiPoslodavca SET RadnikId = @nid WHERE RadnikId = @sid AND Godina = @g AND Mesec = @m", connection, transaction)) {
                u3.Parameters.AddWithValue("@nid", radnikId);
                u3.Parameters.AddWithValue("@sid", stariRadnikId);
                u3.Parameters.AddWithValue("@g", god);
                u3.Parameters.AddWithValue("@m", mes);
                u3.ExecuteNonQuery();
            }

            updatedObracuniCount++;
        }

    } catch (Exception ex) {
        Console.WriteLine($"Error reading row: {ex.Message}");
    }
}

transaction.Commit();

Console.WriteLine($"Ubačeno novih istorijskih radnika: {insertedCount}");
Console.WriteLine($"Ažurirano obračuna da pokazuju na ispravne radnike: {updatedObracuniCount}");
