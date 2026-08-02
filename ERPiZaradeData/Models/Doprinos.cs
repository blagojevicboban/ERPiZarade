using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ERPiZaradeData.Models;

/// <summary>Sistemski doprinosi i stope — port DOPRINOS.DBF + DOPRINOI.DBF</summary>
[Table("Doprinosi")]
public class Doprinos
{
    [Key]
    public int Id { get; set; }

    public int Godina { get; set; }
    public int Mesec { get; set; }
    public int RedniBroj { get; set; }

    [MaxLength(60)]
    public string Naziv { get; set; } = "";

    [Column(TypeName = "decimal(6,3)")]
    public decimal ProcRadn { get; set; } // % na zaradu - radnik

    [Column(TypeName = "decimal(6,3)")]
    public decimal ProcPosl { get; set; } // % na zaradu - poslodavac

    [Column(TypeName = "decimal(6,3)")]
    public decimal B60ProcR { get; set; } // % na bol.do 30 - radnik

    [Column(TypeName = "decimal(6,3)")]
    public decimal B60ProcP { get; set; } // % na bol.do 30 - poslodavac

    [Column(TypeName = "decimal(6,2)")]
    public decimal Bp60ProcP { get; set; } // % na bol.preko 30 - poslodavac

    [Column(TypeName = "decimal(6,2)")]
    public decimal Bp60FProcP { get; set; } // % na bol preko 30 - fond

    [Column(TypeName = "decimal(6,2)")]
    public decimal PorProcP { get; set; }

    [Column(TypeName = "decimal(6,2)")]
    public decimal NepProcP { get; set; } // % neplac. do 30 - poslodavac

    [Column(TypeName = "decimal(6,2)")]
    public decimal InvProcP { get; set; } // % na inval. II kat. - fond

    [MaxLength(60)]
    public string Svrha1 { get; set; } = ""; // Svrha - prvi red

    [MaxLength(60)]
    public string Svrha2 { get; set; } = ""; // Svrha - drugi red

    [MaxLength(60)]
    public string Primalac1 { get; set; } = ""; // Primalac - prvi red

    [MaxLength(60)]
    public string Primalac2 { get; set; } = ""; // Primalac - drugi red

    [MaxLength(40)]
    public string ZiroRacun { get; set; } = ""; // Žiro račun radnik / primalac 1

    [MaxLength(40)]
    public string ZiroRacP { get; set; } = ""; // Žiro račun poslodavac

    [MaxLength(30)]
    public string PozivNaB { get; set; } = ""; // Poziv na broj 1

    [MaxLength(30)]
    public string PozivNa2 { get; set; } = ""; // Poziv na broj 2

    [MaxLength(10)]
    public string SifPlac { get; set; } = ""; // Šifra plaćanja 1

    [MaxLength(10)]
    public string SifPlacP { get; set; } = ""; // Šifra plaćanja 2

    [Column(TypeName = "decimal(14,2)")]
    public decimal NajnizaOsnovica { get; set; } // Najniža bruto osnovica

    [Column(TypeName = "decimal(14,2)")]
    public decimal NajvisaOsnovica { get; set; } // Najviša bruto osnovica
}
