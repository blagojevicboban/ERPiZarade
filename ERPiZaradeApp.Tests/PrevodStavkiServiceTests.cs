using Microsoft.EntityFrameworkCore;
using ERPiZaradeApp.Services;
using ERPiZaradeData;
using ERPiZaradeData.Models;

namespace ERPiZaradeApp.Tests;

/// <summary>
/// Prevođenje zatečenih obračuna na model stavki radi nad podacima koji su već isplaćeni,
/// pa je pravilo strogo: obračun se prevodi samo ako se zbir stavki poklopi sa bruto
/// iznosom. Delimično preveden obračun izgleda ispravno, a daje pogrešan listić.
/// </summary>
public class PrevodStavkiServiceTests
{
    private const int Godina = 2025;
    private const int Mesec = 6;

    private static PlataDbContext NoviKontekst(bool saSifarnikom = true)
    {
        var options = new DbContextOptionsBuilder<PlataDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var db = new PlataDbContext(options);
        if (saSifarnikom) db.VrstePrimanja.AddRange(VrstePrimanjaSeed.Podrazumevane());
        db.SaveChanges();
        return db;
    }

    /// <summary>
    /// Zatečeni obračun onakav kakav ga je pisala ranija verzija: kolone „Neto*" nose
    /// bruto iznose, a `Neto` nosi ukupan bruto.
    /// </summary>
    private static ObracunPlate DodajZatecen(
        PlataDbContext db,
        int id = 1,
        Action<ObracunPlate>? podesi = null)
    {
        db.Radnici.Add(new Radnik
        {
            Id = id,
            BrojRadnika = id,
            ImeIPrezime = $"Radnik {id}",
            Godina = Godina,
            Mesec = Mesec
        });

        var obracun = new ObracunPlate
        {
            Id = id,
            RadnikId = id,
            Godina = Godina,
            Mesec = Mesec,
            NetoZar = 64000m,        // osnovna zarada
            RedovniSati = 160,
            BrutoMinuliRad = 6400m,
            NetoPrek = 4032m,
            PrekovremeneSati = 8,
            Prosek = 400m
        };

        podesi?.Invoke(obracun);

        // Ukupan bruto se drži u koloni `Neto`, kako ga je pisala ranija verzija.
        if (obracun.Neto == 0m)
            obracun.Neto = obracun.NetoZar + obracun.BrutoMinuliRad + obracun.NetoPrek;

        db.ObracuniPlata.Add(obracun);
        db.SaveChanges();
        return obracun;
    }

    [Fact]
    public void Prevedi_ZatecenObracun_DobijaStavkeKojeSeZbirajuNaBruto()
    {
        using var db = NoviKontekst();
        var obracun = DodajZatecen(db);

        var rezultat = new PrevodStavkiService(db).Prevedi();

        Assert.Equal(1, rezultat.Prevedeno);
        Assert.True(rezultat.JeCist);

        var stavke = db.ObracunStavke.Where(s => s.ObracunPlateId == obracun.Id).ToList();
        Assert.Equal(3, stavke.Count);
        Assert.Equal(obracun.Neto, stavke.Sum(s => s.Iznos));
    }

    [Fact]
    public void Prevedi_PreslikavaSateUzIznose()
    {
        using var db = NoviKontekst();
        DodajZatecen(db);

        new PrevodStavkiService(db).Prevedi();

        int zaradaId = db.VrstePrimanja.Single(v => v.Sifra == VrstePrimanjaSeed.OsnovnaZarada).VrstaPrimanjaId;
        var stavka = db.ObracunStavke.Single(s => s.VrstaPrimanjaId == zaradaId);

        Assert.Equal(64000m, stavka.Iznos);
        Assert.Equal(160, stavka.Sati);
    }

    /// <summary>
    /// Bolovanje preko 30 dana i porodiljsko imaju sačuvane sate, ali nikad nisu dobili
    /// kolonu sa iznosom — rekonstruišu se formulom sati × prosek.
    /// </summary>
    [Fact]
    public void Prevedi_KomponenteBezSopstveneKolone_SeRekonstruisuIzSatiIProseka()
    {
        using var db = NoviKontekst();
        DodajZatecen(db, podesi: o =>
        {
            o.BolovanjePreko60SatiLegacy = 40m;
            o.Neto = 64000m + 6400m + 4032m + (40m * 400m);
        });

        var rezultat = new PrevodStavkiService(db).Prevedi();

        Assert.True(rezultat.JeCist);

        int b60 = db.VrstePrimanja.Single(v => v.Sifra == VrstePrimanjaSeed.BolovanjePreko30).VrstaPrimanjaId;
        var stavka = db.ObracunStavke.Single(s => s.VrstaPrimanjaId == b60);

        Assert.Equal(16000m, stavka.Iznos);
        Assert.Equal(40, stavka.Sati);
    }

    /// <summary>Nepokriven deo bruto iznosa zaustavlja prevod tog obračuna.</summary>
    [Fact]
    public void Prevedi_NepokrivenDeoBruta_NePrevodiIPrijavljuje()
    {
        using var db = NoviKontekst();
        DodajZatecen(db, podesi: o => o.Neto = 100000m);   // veće od zbira kolona

        var rezultat = new PrevodStavkiService(db).Prevedi();

        Assert.Equal(0, rezultat.Prevedeno);
        Assert.False(rezultat.JeCist);
        Assert.Empty(db.ObracunStavke);

        var nalaz = Assert.Single(rezultat.Neslaganja);
        Assert.Equal(1, nalaz.BrojRadnika);
        Assert.True(nalaz.Razlika > 0);
    }

    /// <summary>Zaokruživanje pojedinačnih komponenti sme da odstupi za nekoliko para.</summary>
    [Fact]
    public void Prevedi_SitnoOdstupanjeZaokruzivanja_NeZaustavljaPrevod()
    {
        using var db = NoviKontekst();
        DodajZatecen(db, podesi: o => o.Neto = 64000m + 6400m + 4032m + 0.03m);

        var rezultat = new PrevodStavkiService(db).Prevedi();

        Assert.Equal(1, rezultat.Prevedeno);
        Assert.True(rezultat.JeCist);
    }

    /// <summary>Ponovno pokretanje ne sme da udvostruči stavke.</summary>
    [Fact]
    public void Prevedi_PokrenutDvaput_JeIdempotentan()
    {
        using var db = NoviKontekst();
        DodajZatecen(db);

        var servis = new PrevodStavkiService(db);
        servis.Prevedi();
        var drugiPut = servis.Prevedi();

        Assert.Equal(0, drugiPut.Prevedeno);
        Assert.Equal(1, drugiPut.VecImajuStavke);
        Assert.Equal(3, db.ObracunStavke.Count());
    }

    /// <summary>Provera pokazuje šta bi se desilo, ali ništa ne upisuje.</summary>
    [Fact]
    public void Proveri_NistaNeUpisuje()
    {
        using var db = NoviKontekst();
        DodajZatecen(db);

        var rezultat = new PrevodStavkiService(db).Proveri();

        Assert.Equal(1, rezultat.Prevedeno);
        Assert.Empty(db.ObracunStavke);
    }

    [Fact]
    public void Prevedi_SamoIzabranaGodina()
    {
        using var db = NoviKontekst();
        DodajZatecen(db, id: 1);

        db.Radnici.Add(new Radnik { Id = 2, BrojRadnika = 2, ImeIPrezime = "Drugi", Godina = 2024, Mesec = 1 });
        db.ObracuniPlata.Add(new ObracunPlate
        {
            Id = 2, RadnikId = 2, Godina = 2024, Mesec = 1,
            NetoZar = 50000m, RedovniSati = 160, Neto = 50000m
        });
        db.SaveChanges();

        var rezultat = new PrevodStavkiService(db).Prevedi(Godina);

        Assert.Equal(1, rezultat.Prevedeno);
        Assert.Equal(1, rezultat.UkupnoObracuna);
    }

    [Fact]
    public void Prevedi_PrazanSifarnik_Prijavljuje()
    {
        using var db = NoviKontekst(saSifarnikom: false);
        DodajZatecen(db);

        var rezultat = new PrevodStavkiService(db).Prevedi();

        Assert.False(rezultat.JeCist);
        Assert.Contains(rezultat.Neslaganja, n => n.Opis.Contains("Šifarnik"));
    }
}
