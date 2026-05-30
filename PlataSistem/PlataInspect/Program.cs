using System;
using System.IO;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using PlataData;
using PlataData.Models;

namespace PlataInspect
{
    class Program
    {
        static void Main(string[] args)
        {
            string dbPath = @"C:\PLATA\PlataSistem\Baze\firma_100188310_PSSS_PIROT_DOO_PIROT.db";
            Console.WriteLine($"Database Path: {dbPath}");
            if (!File.Exists(dbPath))
            {
                Console.WriteLine("Database file not found!");
                return;
            }

            using var db = PlataDbContext.Create(dbPath);

            Console.WriteLine("--- SEARCHING OBRACUN FOR OLIVERA IN 2026 ---");
            var query = db.ObracuniPlata
                .Include(o => o.Radnik)
                .Where(o => o.Radnik.Jmbg == "2004974755044" && o.Godina == 2026)
                .OrderBy(o => o.Mesec)
                .ToList();

            foreach (var o in query)
            {
                var porez = db.Porezi.FirstOrDefault(p => p.Godina == o.Godina && p.Mesec == o.Mesec);
                decimal vrboda = porez?.VrBoda ?? 0;
                Console.WriteLine($"Period: {o.Mesec}.{o.Godina} | CenaSataRedovan: {o.CenaSataRedovan} | Koef: {o.Koeficijent} | FondSatiMesecni: {o.FondSatiMesecni} | RedovniSati: {o.RedovniSati} | VrBoda: {vrboda}");
            }
        }
    }
}
