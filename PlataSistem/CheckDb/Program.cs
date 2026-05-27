using System;
using Microsoft.Data.Sqlite;

var dbPath = @"C:\PLATA\PlataSistem\plata.db";
Console.WriteLine($"Primeri obracuna iz tabele ObracuniPlata u bazi: {dbPath}\n");

using var conn = new SqliteConnection($"Data Source={dbPath}");
conn.Open();

using var cmd = conn.CreateCommand();
cmd.CommandText = @"
    SELECT o.Godina, o.Mesec, o.BrutoZarada, o.BrutoBolovanje, 
           o.DoprinosPioPoslodavac, o.DoprinosZdravstvoPoslodavac, o.DoprinosNezaposlenostPoslodavac,
           o.RadnikId
    FROM ObracuniPlata o
    ORDER BY o.Godina DESC, o.Mesec DESC, o.Id
    LIMIT 30";

using var reader = cmd.ExecuteReader();

Console.WriteLine(string.Format("{0,-10} | {1,-10} | {2,-12} | {3,-12} | {4,-12} | {5,-12}", "Period", "RadnikId", "Bruto", "PIO Posl", "Zdr Posl", "Nez Posl"));
Console.WriteLine(new string('-', 80));

while (reader.Read())
{
    int godina = reader.GetInt32(0);
    int mesec = reader.GetInt32(1);
    decimal bruto = reader.GetDecimal(2) + reader.GetDecimal(3);
    decimal pio = reader.GetDecimal(4);
    decimal zdr = reader.GetDecimal(5);
    decimal nez = reader.GetDecimal(6);
    int radnikId = reader.GetInt32(7);

    string strPeriod = $"{mesec:D2}/{godina}";
    Console.WriteLine(string.Format("{0,-10} | {1,-10} | {2,-12:N2} | {3,-12:N2} | {4,-12:N2} | {5,-12:N2}", strPeriod, radnikId, bruto, pio, zdr, nez));
}
