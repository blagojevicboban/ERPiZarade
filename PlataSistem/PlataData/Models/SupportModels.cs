using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PlataData.Models;

/// <summary>Kredit ili obustava — port KREDIT.DBF</summary>
[Table("Krediti")]
public class Kredit
{
    [Key] public int Id { get; set; }
    [ForeignKey(nameof(Radnik))] public int RadnikId { get; set; }

    [MaxLength(60)] public string Opis { get; set; } = "";

    [Column(TypeName = "decimal(14,2)")] public decimal UkupanIznos { get; set; }
    [Column(TypeName = "decimal(14,2)")] public decimal MesecnaRata { get; set; }
    [Column(TypeName = "decimal(14,2)")] public decimal OstatakDuga { get; set; }

    public int BrojRata { get; set; }
    public int PlateneRate { get; set; }
    public DateTime DatumPocetka { get; set; }
    public DateTime? DatumZavrsetka { get; set; }
    public bool Aktivan { get; set; } = true;

    public Radnik Radnik { get; set; } = null!;
}

/// <summary>Radni sati — port RAD_SATI.DBF</summary>
[Table("RadniSati")]
public class RadniSat
{
    [Key] public int Id { get; set; }
    [ForeignKey(nameof(Radnik))] public int RadnikId { get; set; }
    public int Godina { get; set; }
    public int Mesec { get; set; }

    public int RedovniSati { get; set; }
    public int BolovanjeSati { get; set; }
    public int PrekovremeneSati { get; set; }
    public int GodisnjiOdmorSati { get; set; }
    public int DrzavniPraznikSati { get; set; }
    public int NocniSati { get; set; }
    public int SmenskiSati { get; set; }
    public int RadPraznikomSati { get; set; }
    public int NocniRadPraznikomSati { get; set; }
    public int PlacenoOdsustvoSati { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal Stimulacija { get; set; }

    public int RadNedeljomSati { get; set; }
    public int PlacenoZakonskiSati { get; set; }
    public int BolovanjePreko60Sati { get; set; }
    public int PorodiljskoOdsustvoSati { get; set; }
    public int Bolovanje100Sati { get; set; }
    public int TopliObrokDani { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal RegresIznos { get; set; }

    [Column(TypeName = "decimal(14,4)")]
    public decimal Prosek { get; set; }

    public Radnik Radnik { get; set; } = null!;
}

/// <summary>Poreske stope i razredi — port POREZI.DBF + RAZREDI.DBF</summary>
[Table("PoreskeStope")]
public class PoreznaStopa
{
    [Key] public int Id { get; set; }
    public int RedniBroj { get; set; }
    [Column(TypeName = "decimal(14,2)")] public decimal GranjaOd { get; set; }
    [Column(TypeName = "decimal(14,2)")] public decimal GranicaDo { get; set; }
    [Column(TypeName = "decimal(6,4)")] public decimal Stopa { get; set; }
    [Column(TypeName = "decimal(14,2)")] public decimal FiksniIznos { get; set; }
    public int GodisnjuVazenja { get; set; }
    public int MesecVazenja { get; set; }
}

/// <summary>Kategorije radnika — port KATEGORI.DBF</summary>
[Table("Kategorije")]
public class Kategorija
{
    [Key] public int Id { get; set; }
    [MaxLength(10)] public string Sifra { get; set; } = "";
    [MaxLength(60)] public string Naziv { get; set; } = "";
    [Column(TypeName = "decimal(8,4)")] public decimal Koeficijent { get; set; }
    [Column(TypeName = "decimal(6,4)")] public decimal StopaPio { get; set; }
    [Column(TypeName = "decimal(6,4)")] public decimal StopaZdravstvo { get; set; }
}

/// <summary>Samodoprinosi — port SAMODOP.DBF</summary>
[Table("Samodoprinosi")]
public class Samodoprinosi
{
    [Key] public int Id { get; set; }
    [ForeignKey(nameof(Radnik))] public int RadnikId { get; set; }
    public int Godina { get; set; }
    public int Mesec { get; set; }
    [Column(TypeName = "decimal(14,2)")] public decimal Iznos { get; set; }
    [MaxLength(60)] public string Opis { get; set; } = "";
    public Radnik Radnik { get; set; } = null!;
}

/// <summary>Normativ bodova/sati — port NORMATIV.DBF</summary>
[Table("Normativi")]
public class Normativ
{
    [Key] public int Id { get; set; }
    [MaxLength(20)] public string Sifra { get; set; } = "";
    [MaxLength(60)] public string Naziv { get; set; } = "";
    [Column(TypeName = "decimal(10,4)")] public decimal VrednostBoda { get; set; }
    public char Tip { get; set; } = 'P'; // P=procenat, L=linearno, S=stimulacija, B=bodovi, C=casovi
}

/// <summary>Platni razredi (najnize bruto osnovice za stepene strucne spreme) - port RAZREDI.DBF</summary>
[Table("PlatniRazredi")]
public class PlatniRazred
{
    [Key]
    public int Id { get; set; }

    // Normalni doprinosi
    [Column(TypeName = "decimal(14,2)")] public decimal R1 { get; set; }
    [Column(TypeName = "decimal(14,2)")] public decimal R2 { get; set; }
    [Column(TypeName = "decimal(14,2)")] public decimal R3 { get; set; }
    [Column(TypeName = "decimal(14,2)")] public decimal R4 { get; set; }
    [Column(TypeName = "decimal(14,2)")] public decimal R5 { get; set; }
    [Column(TypeName = "decimal(14,2)")] public decimal R6 { get; set; }
    [Column(TypeName = "decimal(14,2)")] public decimal R7 { get; set; }
    [Column(TypeName = "decimal(14,2)")] public decimal R8 { get; set; }
    [Column(TypeName = "decimal(14,2)")] public decimal R9 { get; set; }

    // Doprinosi za PIO
    [Column(TypeName = "decimal(14,2)")] public decimal P1 { get; set; }
    [Column(TypeName = "decimal(14,2)")] public decimal P2 { get; set; }
    [Column(TypeName = "decimal(14,2)")] public decimal P3 { get; set; }
    [Column(TypeName = "decimal(14,2)")] public decimal P4 { get; set; }
    [Column(TypeName = "decimal(14,2)")] public decimal P5 { get; set; }
    [Column(TypeName = "decimal(14,2)")] public decimal P6 { get; set; }
    [Column(TypeName = "decimal(14,2)")] public decimal P7 { get; set; }
    [Column(TypeName = "decimal(14,2)")] public decimal P8 { get; set; }
    [Column(TypeName = "decimal(14,2)")] public decimal P9 { get; set; }
}
