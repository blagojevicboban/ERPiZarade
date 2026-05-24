using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PlataData.Models;

/// <summary>Sistemski parametri, porezi i procenti uvećanja — port POREZI.DBF + POREZII.DBF</summary>
[Table("Porezi")]
public class Porezi
{
    [Key]
    public int Id { get; set; }

    public int Godina { get; set; }
    public int Mesec { get; set; }
    public int RedniBroj { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal Zarada { get; set; } // Garantovana zarada

    [Column(TypeName = "decimal(6,2)")]
    public decimal AkPorez { get; set; } // 1. stopa poreza %

    [Column(TypeName = "decimal(6,2)")]
    public decimal AkPorez2 { get; set; } // % por > 60000 %

    [Column(TypeName = "decimal(6,2)")]
    public decimal AkPorez3 { get; set; } // % por > 100000 %

    [Column(TypeName = "decimal(6,2)")]
    public decimal AkPorez4 { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal Prvast { get; set; } // Gornja granica 1. stope poreza (poresko oslobodjenje)

    [Column(TypeName = "decimal(14,2)")]
    public decimal Drugast { get; set; } // Gornja granica 2. stope poreza

    [Column(TypeName = "decimal(14,2)")]
    public decimal Trecast { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal LinPorez3 { get; set; }

    // Osnovni porez
    [MaxLength(10)]
    public string SifPlac1 { get; set; } = "";

    [MaxLength(40)]
    public string ZiroR1 { get; set; } = "";

    [MaxLength(20)]
    public string PozivNa1 { get; set; } = "";

    [MaxLength(20)]
    public string PozivNa3 { get; set; } = "";

    [MaxLength(60)]
    public string Svrha1 { get; set; } = "";

    [MaxLength(60)]
    public string Svrha2 { get; set; } = "";

    [MaxLength(60)]
    public string Primalac1 { get; set; } = "";

    [MaxLength(60)]
    public string Primalac2 { get; set; } = "";

    // Dodatni porez
    [MaxLength(10)]
    public string SifPlac2 { get; set; } = "";

    [MaxLength(40)]
    public string ZiroR2 { get; set; } = "";

    [MaxLength(20)]
    public string PozivNa2 { get; set; } = "";

    [MaxLength(20)]
    public string PozivNa4 { get; set; } = "";

    [Column(TypeName = "decimal(6,2)")]
    public decimal PosPorez { get; set; } // Poseban / dodatni porez

    [MaxLength(60)]
    public string Svrha3 { get; set; } = "";

    [MaxLength(60)]
    public string Svrha4 { get; set; } = "";

    [MaxLength(60)]
    public string Primalac3 { get; set; } = "";

    [MaxLength(60)]
    public string Primalac4 { get; set; } = "";

    // Procenti uvećanja i naknada
    [Column(TypeName = "decimal(6,2)")]
    public decimal ProcDrzav { get; set; } // % uvećanja za rad državnim praznikom

    [Column(TypeName = "decimal(6,2)")]
    public decimal ProcNocni { get; set; } // % uvećanja za noćni rad

    [Column(TypeName = "decimal(6,2)")]
    public decimal ProcPreko { get; set; } // % uvećanja za prekovremeni rad

    [Column(TypeName = "decimal(6,2)")]
    public decimal ProcMinul { get; set; } // % uvećanja za minuli rad godišnje

    [Column(TypeName = "decimal(6,2)")]
    public decimal ProcNedel { get; set; } // % naknade za rad nedeljom

    [Column(TypeName = "decimal(6,2)")]
    public decimal ProcBolov { get; set; } // % plaćanja bolovanja

    [Column(TypeName = "decimal(6,2)")]
    public decimal ProcPlac { get; set; } // % plaćanja plaćenog odsustva

    [Column(TypeName = "decimal(6,2)")]
    public decimal ProcPlZa { get; set; } // % plaćanja zakonskog plaćenog odsustva

    [Column(TypeName = "decimal(6,2)")]
    public decimal ProcInval { get; set; } // % naknade za invalide II kat. - socijalno

    // Ostalo
    public int FondCasova { get; set; }
    public int CasZaOb { get; set; }

    [Column(TypeName = "decimal(10,4)")]
    public decimal VrBoda { get; set; }

    [Column(TypeName = "decimal(6,2)")]
    public decimal ProcIzdrz { get; set; }

    [MaxLength(10)]
    public string Akont { get; set; } = "DA";

    [Column(TypeName = "decimal(14,2)")]
    public decimal ProsBrut { get; set; }
}
