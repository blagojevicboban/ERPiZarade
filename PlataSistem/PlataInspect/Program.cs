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
            string dbPath = @"C:\PLATA\PlataSistem\plata.db";
            using var db = PlataDbContext.Create(dbPath);

            int brojRadnika = 4; // Vidanovic Srdjan
            int godina = 2026;
            int mesec = 4;

            // --- Ucitaj podatke ---
            var radnik = db.Radnici
                .Where(r => r.BrojRadnika == brojRadnika)
                .OrderByDescending(r => r.Godina).ThenByDescending(r => r.Mesec)
                .FirstOrDefault();

            var obracun = db.ObracuniPlata
                .Include(o => o.Radnik)
                .Where(o => o.Radnik.BrojRadnika == brojRadnika && o.Godina == godina && o.Mesec == mesec)
                .FirstOrDefault();

            var sati = db.RadniSati
                .Include(s => s.Radnik)
                .Where(s => s.Radnik.BrojRadnika == brojRadnika && s.Godina == godina && s.Mesec == mesec)
                .FirstOrDefault();

            var porezi = db.Porezi
                .Where(p => p.Godina == godina && p.Mesec == mesec)
                .OrderBy(p => p.RedniBroj)
                .FirstOrDefault()
                ?? db.Porezi
                    .Where(p => p.Godina < godina || (p.Godina == godina && p.Mesec < mesec))
                    .OrderByDescending(p => p.Godina).ThenByDescending(p => p.Mesec)
                    .FirstOrDefault();

            if (radnik == null || obracun == null || porezi == null)
            {
                Console.WriteLine("Nedostaju podaci.");
                return;
            }

            Console.WriteLine("====================================================================");
            Console.WriteLine($"  Provera obracuna minulog rada: {radnik.ImeIPrezime} ({mesec:D2}/{godina})");
            Console.WriteLine("====================================================================");

            // --- Ulazni parametri ---
            decimal koeficijent = obracun.Koeficijent > 0 ? obracun.Koeficijent : radnik.Koeficijent;
            decimal vrednostBoda = porezi.VrednostBoda > 0 ? porezi.VrednostBoda : 1860.34m;
            int fondCasova = obracun.FondSatiMesecni > 0 ? obracun.FondSatiMesecni : 160;
            decimal procMinul = porezi.ProcMinul; // % uvecanja po godini staza
            int godinePraxis = obracun.MinuliRadGodine > 0 ? obracun.MinuliRadGodine : radnik.MinuliRadGodine;

            // Radni sati (iz RadniSati tabele, ili iz Obracuna ako nema)
            decimal redovniSati   = sati?.RedovniSati   ?? obracun.RedovniSati;
            decimal prekovremeni  = sati?.PrekovremeneSati ?? obracun.PrekovremeneSati;
            decimal radPraznikom  = sati?.RadPraznikomSati ?? obracun.RadPraznikomSati;
            decimal nocni         = sati?.NocniSati      ?? obracun.NocniSati;
            decimal nedeljom      = sati?.RadNedeljomSati ?? obracun.NedeljaSati;

            Console.WriteLine("\n--- Ulazni parametri ---");
            Console.WriteLine($"  Koeficijent            : {koeficijent:F2}");
            Console.WriteLine($"  Vrednost boda          : {vrednostBoda:F2}");
            Console.WriteLine($"  Fond casova (mesec)    : {fondCasova}");
            Console.WriteLine($"  ProcMinul (po god.)    : {procMinul:F4} %");
            Console.WriteLine($"  Godine staza           : {godinePraxis}");
            Console.WriteLine($"  Redovni sati           : {redovniSati:F2}");
            Console.WriteLine($"  Prekovremeni sati      : {prekovremeni:F2}");
            Console.WriteLine($"  Rad praznikom sati     : {radPraznikom:F2}");
            Console.WriteLine($"  Nocni sati             : {nocni:F2}");
            Console.WriteLine($"  Nedeljom sati          : {nedeljom:F2}");

            // --- STARI PROGRAM (OBRAC.PRG) ---
            // neto_zar = uk_r_sati * koefic * vred_boda / f_casova
            // uk_r_sati = radn_sati + drzavni + nocni + prekovreme + nedelja  (bez stimulacije u ovom slucaju)
            // min_rad_iz = neto_zar * pr_minul * min_rad / 100
            decimal oldUkRSati = redovniSati + radPraznikom + nocni + prekovremeni + nedeljom;
            decimal oldNetoZar = oldUkRSati * koeficijent * vrednostBoda / fondCasova;
            decimal oldMinRadIz = oldNetoZar * procMinul * godinePraxis / 100m;

            Console.WriteLine("\n--- STARI PROGRAM (OBRAC.PRG logika) ---");
            Console.WriteLine($"  uk_r_sati (radni+prz+noc+prek+ned) = {oldUkRSati:F2}");
            Console.WriteLine($"  neto_zar  = {oldUkRSati} * {koeficijent} * {vrednostBoda} / {fondCasova}");
            Console.WriteLine($"  neto_zar  = {oldNetoZar:F4}");
            Console.WriteLine($"  min_rad_iz = neto_zar * procMinul * god_staza / 100");
            Console.WriteLine($"  min_rad_iz = {oldNetoZar:F4} * {procMinul:F4} * {godinePraxis} / 100");
            Console.WriteLine($"  min_rad_iz = {oldMinRadIz:F4}");
            Console.WriteLine($"  min_rad_iz (zaokruzeno) = {Math.Round(oldMinRadIz, 2):F2}");

            // --- NOVI PROGRAM (ObracunService.cs) ---
            // hourlyBase = koeficijent * vrednostBoda / fondCasova
            // workedHours = redovni + prekovremeni + radPraznikom + nocni + nedeljom
            // neto_zar = workedHours * hourlyBase
            // brutoMinuliRad = neto_zar * (procMinul / 100) * yearsOfTenure
            decimal newHourlyBase  = koeficijent * vrednostBoda / fondCasova;
            decimal newWorkedHours = redovniSati + prekovremeni + radPraznikom + nocni + nedeljom;
            decimal newNetoZar     = newWorkedHours * newHourlyBase;
            decimal newMinRad      = Math.Round(newNetoZar * (procMinul / 100m) * godinePraxis, 2);

            Console.WriteLine("\n--- NOVI PROGRAM (ObracunService.cs logika) ---");
            Console.WriteLine($"  hourlyBase  = {koeficijent} * {vrednostBoda} / {fondCasova} = {newHourlyBase:F6}");
            Console.WriteLine($"  workedHours = {newWorkedHours:F2}");
            Console.WriteLine($"  neto_zar    = {newWorkedHours} * {newHourlyBase:F6} = {newNetoZar:F4}");
            Console.WriteLine($"  brutoMinuliRad = {newNetoZar:F4} * ({procMinul:F4}/100) * {godinePraxis}");
            Console.WriteLine($"  brutoMinuliRad = {newMinRad:F2}");

            // --- Vrednost iz baze ---
            Console.WriteLine("\n--- VREDNOST IZ BAZE (obracun.BrutoMinuliRad) ---");
            Console.WriteLine($"  obracun.BrutoMinuliRad = {obracun.BrutoMinuliRad:F2}");
            Console.WriteLine($"  obracun.NetoZar        = {obracun.NetoZar:F2}   (ovo je osnova starog programa)");
            Console.WriteLine($"  obracun.CenaSataRedovan = {obracun.CenaSataRedovan:F6}");
            Console.WriteLine($"  obracun.CenaSataMinuliRad = {obracun.CenaSataMinuliRad:F6}");

            // --- Poređenje ---
            Console.WriteLine("\n====================================================================");
            Console.WriteLine("  POREĐENJE:");
            Console.WriteLine($"  Stari program  (preracunato) : {Math.Round(oldMinRadIz, 2):F2}");
            Console.WriteLine($"  Novi program   (preracunato) : {newMinRad:F2}");
            Console.WriteLine($"  Baza           (snimljeno)   : {obracun.BrutoMinuliRad:F2}");
            Console.WriteLine($"  Razlika (stari vs. novi)     : {Math.Round(oldMinRadIz - newMinRad, 2):F2}");
            Console.WriteLine($"  Razlika (novi vs. baza)      : {Math.Round(newMinRad - obracun.BrutoMinuliRad, 2):F2}");
            Console.WriteLine("====================================================================");
        }
    }
}
