using System;
using System.IO;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using PlataData;
using PlataData.Models;
using System.Collections.Generic;

namespace PlataInspect
{
    class Program
    {
        static void Main(string[] args)
        {
            string dbPath = @"C:\PLATA\PlataSistem\Baze\firma_100188310_PSSS_PIROT_DOO_PIROT.db";
            if (!File.Exists(dbPath)) return;

            using var db = PlataDbContext.Create(dbPath);
            int radnikId = 19; // Nikolic Zoran
            int godina = 2026;
            int mesec = 5;

            var targetPeriods = new List<(int Year, int Month)>();
            int currentYear = godina;
            int currentMonth = mesec;

            for (int i = 1; i <= 12; i++)
            {
                int m = currentMonth - i;
                int y = currentYear;
                while (m <= 0)
                {
                    m += 12;
                    y -= 1;
                }
                targetPeriods.Add((y, m));
            }

            var targetRadnik = db.Radnici.Find(radnikId);
            int targetBrojRadnika = targetRadnik.BrojRadnika;

            decimal psumbr = 0m;
            decimal psumcas = 0m;

            var obracuni = db.ObracuniPlata
                .Include(o => o.Radnik)
                .Where(o => o.Radnik.BrojRadnika == targetBrojRadnika)
                .ToList()
                .Where(o => targetPeriods.Any(p => p.Year == o.Godina && p.Month == o.Mesec))
                .ToList();

            var satiLista = db.RadniSati
                .Include(s => s.Radnik)
                .Where(s => s.Radnik.BrojRadnika == targetBrojRadnika)
                .ToList()
                .Where(s => targetPeriods.Any(p => p.Year == s.Godina && p.Month == s.Mesec))
                .ToDictionary(s => (s.Godina, s.Mesec));

            Console.WriteLine("Period | Casovi | TotalGross | NonWorkedGross | RegularGross");
            foreach (var ob in obracuni)
            {
                decimal casovi = 0;
                if (satiLista.TryGetValue((ob.Godina, ob.Mesec), out var s))
                {
                    casovi = s.RedovniSati + s.PrekovremeneSati + s.RadPraznikomSati + s.NocniSati + s.RadNedeljomSati;
                }
                else
                {
                    casovi = ob.RedovniSati + ob.PrekovremeneSati + ob.RadPraznikomSati + ob.NocniSati + ob.NedeljaSati;
                }

                decimal totalGross = ob.BrutoZarada + ob.BrutoBolovanje;
                decimal nonWorkedGross = ob.BrutoBolovanje + ob.NetoGOd + ob.NetoNerd + ob.NetoB100 + ob.NetoPlac + ob.NetoPlZ 
                                         + (ob.BolovanjePreko60SatiLegacy * ob.Prosek) 
                                         + (ob.PorodiljskoOdsustvoSatiLegacy * ob.Prosek);
                decimal regularGross = Math.Max(0, totalGross - nonWorkedGross);

                Console.WriteLine($"{ob.Mesec:D2}.{ob.Godina} | Casovi: {casovi} | TotalBruto: {totalGross} | NonWorked: {nonWorkedGross} | RegGross: {regularGross}");

                psumbr += regularGross;
                psumcas += casovi;
            }

            Console.WriteLine($"TOTAL worked gross: {psumbr}");
            Console.WriteLine($"TOTAL worked hours: {psumcas}");
            Console.WriteLine($"Calculated Average: {Math.Round(psumbr / psumcas, 4)}");
        }
    }
}
