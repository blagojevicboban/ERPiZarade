using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using ERPiZaradeData;
using ERPiZaradeData.Models;

namespace ERPiZaradeApp.Services;

/// <summary>
/// Rate kredita i obustava vezane za jedan obračun.
///
/// Ista računica je do Faze 2.7 stajala prepisana na dva mesta (prekalkulacija i brisanje
/// perioda), pa je storniranje bilo treće mesto na kom se mogla razići. Ovde je jednom, sa
/// testom, jer greška znači da radniku ostane skinuta rata za platu koja nije isplaćena —
/// ili da mu se ista rata skine dvaput.
/// </summary>
public static class KreditRateService
{
    /// <summary>
    /// Da li je obračun uopšte skinuo rate (Faza 2.2). Akontacija, bonus i 13. plata se
    /// isplaćuju bez obustava, pa im se rata ni ne vraća — vraćanje neskinute rate bi
    /// radniku smanjilo dug bez ijednog dinara koji je otišao poveriocu.
    ///
    /// Obračun bez isplate pripada prvoj isplati meseca i radi kao pre Faze 2.2.
    /// </summary>
    private static bool NosiObustave(PlataDbContext db, ObracunPlate obracun)
    {
        if (obracun.IsplataId == null) return true;

        var isplata = db.Isplate.FirstOrDefault(i => i.IsplataId == obracun.IsplataId);
        return isplata == null || isplata.NosiObustave;
    }

    /// <summary>
    /// Vraća ratu koja je bila skinuta u periodu obračuna. Poziva se kad obračun prestaje
    /// da važi — brisanjem, prekalkulacijom ili storniranjem.
    /// </summary>
    /// <returns>Broj kredita kojima je rata vraćena.</returns>
    public static int VratiRate(PlataDbContext db, ObracunPlate obracun)
    {
        if (!NosiObustave(db, obracun)) return 0;

        var periodPocetak = new DateTime(obracun.Godina, obracun.Mesec, 1);
        int pogodjeno = 0;

        foreach (var k in db.Krediti.Where(k => k.RadnikId == obracun.RadnikId).ToList())
        {
            // Rata se vraća samo ako je period obračuna unutar opsega već otplaćenih rata;
            // u suprotnom bi se skidalo sa kredita koji u tom mesecu nije ni naplaćivan.
            if (k.PlateneRate <= 0) continue;
            if (k.DatumPocetka > periodPocetak) continue;
            if (periodPocetak > k.DatumPocetka.AddMonths(k.PlateneRate - 1)) continue;

            k.PlateneRate--;
            k.OstatakDuga = Math.Max(0, k.UkupanIznos - (k.PlateneRate * k.MesecnaRata));
            k.Aktivan = true;
            db.Entry(k).State = EntityState.Modified;
            pogodjeno++;
        }

        return pogodjeno;
    }

    /// <summary>
    /// Ponovo skida rate za period obračuna — suprotno od <see cref="VratiRate"/>.
    /// Koristi se pri poništavanju storniranja, da se vrati stanje pre njega.
    /// </summary>
    /// <returns>Broj kredita kojima je rata skinuta.</returns>
    public static int SkiniRate(PlataDbContext db, ObracunPlate obracun)
    {
        if (!NosiObustave(db, obracun)) return 0;

        var periodPocetak = new DateTime(obracun.Godina, obracun.Mesec, 1);
        int pogodjeno = 0;

        foreach (var k in db.Krediti.Where(k => k.RadnikId == obracun.RadnikId).ToList())
        {
            if (k.DatumPocetka > periodPocetak) continue;
            if (k.OstatakDuga <= 0) continue;
            if (k.BrojRata > 0 && k.PlateneRate >= k.BrojRata) continue;

            k.PlateneRate++;
            k.OstatakDuga = Math.Max(0, k.UkupanIznos - (k.PlateneRate * k.MesecnaRata));
            if (k.OstatakDuga <= 0 || (k.BrojRata > 0 && k.PlateneRate >= k.BrojRata))
            {
                k.Aktivan = false;
            }
            db.Entry(k).State = EntityState.Modified;
            pogodjeno++;
        }

        return pogodjeno;
    }
}
