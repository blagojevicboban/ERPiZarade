using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PlataData.Models;

/// <summary>
/// Detaljni doprinosi na teret poslodavca — port POSL_OBR.DBF (tekući) + POSLOBRI.DBF (istorija)
/// </summary>
[Table("DoprinosiPoslodavca")]
public class DoprinosiPoslodavca
{
    [Key]
    public int Id { get; set; }

    [ForeignKey(nameof(Radnik))]
    public int RadnikId { get; set; }

    public int Godina { get; set; }
    public int Mesec { get; set; }

    // ── ZARADA (1 - 9) ──
    [Column(TypeName = "decimal(14,2)")] public decimal Zar1 { get; set; }
    [Column(TypeName = "decimal(14,2)")] public decimal Zar2 { get; set; }
    [Column(TypeName = "decimal(14,2)")] public decimal Zar3 { get; set; }
    [Column(TypeName = "decimal(14,2)")] public decimal Zar4 { get; set; }
    [Column(TypeName = "decimal(14,2)")] public decimal Zar5 { get; set; }
    [Column(TypeName = "decimal(14,2)")] public decimal Zar6 { get; set; }
    [Column(TypeName = "decimal(14,2)")] public decimal Zar7 { get; set; }
    [Column(TypeName = "decimal(14,2)")] public decimal Zar8 { get; set; }
    [Column(TypeName = "decimal(14,2)")] public decimal Zar9 { get; set; }

    // ── BOLOVANJE DO 30 DANA (1 - 9) ──
    [Column(TypeName = "decimal(14,2)")] public decimal Bol1 { get; set; }
    [Column(TypeName = "decimal(14,2)")] public decimal Bol2 { get; set; }
    [Column(TypeName = "decimal(14,2)")] public decimal Bol3 { get; set; }
    [Column(TypeName = "decimal(14,2)")] public decimal Bol4 { get; set; }
    [Column(TypeName = "decimal(14,2)")] public decimal Bol5 { get; set; }
    [Column(TypeName = "decimal(14,2)")] public decimal Bol6 { get; set; }
    [Column(TypeName = "decimal(14,2)")] public decimal Bol7 { get; set; }
    [Column(TypeName = "decimal(14,2)")] public decimal Bol8 { get; set; }
    [Column(TypeName = "decimal(14,2)")] public decimal Bol9 { get; set; }

    // ── NAKNADE (1 - 9) ──
    [Column(TypeName = "decimal(14,2)")] public decimal Nak1 { get; set; }
    [Column(TypeName = "decimal(14,2)")] public decimal Nak2 { get; set; }
    [Column(TypeName = "decimal(14,2)")] public decimal Nak3 { get; set; }
    [Column(TypeName = "decimal(14,2)")] public decimal Nak4 { get; set; }
    [Column(TypeName = "decimal(14,2)")] public decimal Nak5 { get; set; }
    [Column(TypeName = "decimal(14,2)")] public decimal Nak6 { get; set; }
    [Column(TypeName = "decimal(14,2)")] public decimal Nak7 { get; set; }
    [Column(TypeName = "decimal(14,2)")] public decimal Nak8 { get; set; }
    [Column(TypeName = "decimal(14,2)")] public decimal Nak9 { get; set; }

    // ── OSTALE NAKNADE (NEPUNO RADNO VREME / DRŽAVNI PRAZNIK) (1 - 9) ──
    [Column(TypeName = "decimal(14,2)")] public decimal Nep1 { get; set; }
    [Column(TypeName = "decimal(14,2)")] public decimal Nep2 { get; set; }
    [Column(TypeName = "decimal(14,2)")] public decimal Nep3 { get; set; }
    [Column(TypeName = "decimal(14,2)")] public decimal Nep4 { get; set; }
    [Column(TypeName = "decimal(14,2)")] public decimal Nep5 { get; set; }
    [Column(TypeName = "decimal(14,2)")] public decimal Nep6 { get; set; }
    [Column(TypeName = "decimal(14,2)")] public decimal Nep7 { get; set; }
    [Column(TypeName = "decimal(14,2)")] public decimal Nep8 { get; set; }
    [Column(TypeName = "decimal(14,2)")] public decimal Nep9 { get; set; }

    // ── BOLOVANJE PREKO 30 DANA NA TERET FONDA (B60F) (1 - 9) ──
    [Column(TypeName = "decimal(14,2)")] public decimal B60F1 { get; set; }
    [Column(TypeName = "decimal(14,2)")] public decimal B60F2 { get; set; }
    [Column(TypeName = "decimal(14,2)")] public decimal B60F3 { get; set; }
    [Column(TypeName = "decimal(14,2)")] public decimal B60F4 { get; set; }
    [Column(TypeName = "decimal(14,2)")] public decimal B60F5 { get; set; }
    [Column(TypeName = "decimal(14,2)")] public decimal B60F6 { get; set; }
    [Column(TypeName = "decimal(14,2)")] public decimal B60F7 { get; set; }
    [Column(TypeName = "decimal(14,2)")] public decimal B60F8 { get; set; }
    [Column(TypeName = "decimal(14,2)")] public decimal B60F9 { get; set; }

    // ── BOLOVANJE PREKO 30 DANA NA TERET POSLODAVCA (B60) (1 - 9) ──
    [Column(TypeName = "decimal(14,2)")] public decimal B601 { get; set; }
    [Column(TypeName = "decimal(14,2)")] public decimal B602 { get; set; }
    [Column(TypeName = "decimal(14,2)")] public decimal B603 { get; set; }
    [Column(TypeName = "decimal(14,2)")] public decimal B604 { get; set; }
    [Column(TypeName = "decimal(14,2)")] public decimal B605 { get; set; }
    [Column(TypeName = "decimal(14,2)")] public decimal B606 { get; set; }
    [Column(TypeName = "decimal(14,2)")] public decimal B607 { get; set; }
    [Column(TypeName = "decimal(14,2)")] public decimal B608 { get; set; }
    [Column(TypeName = "decimal(14,2)")] public decimal B609 { get; set; }

    // ── INVALIDI II KATEGORIJE (1 - 9) ──
    [Column(TypeName = "decimal(14,2)")] public decimal Inv1 { get; set; }
    [Column(TypeName = "decimal(14,2)")] public decimal Inv2 { get; set; }
    [Column(TypeName = "decimal(14,2)")] public decimal Inv3 { get; set; }
    [Column(TypeName = "decimal(14,2)")] public decimal Inv4 { get; set; }
    [Column(TypeName = "decimal(14,2)")] public decimal Inv5 { get; set; }
    [Column(TypeName = "decimal(14,2)")] public decimal Inv6 { get; set; }
    [Column(TypeName = "decimal(14,2)")] public decimal Inv7 { get; set; }
    [Column(TypeName = "decimal(14,2)")] public decimal Inv8 { get; set; }
    [Column(TypeName = "decimal(14,2)")] public decimal Inv9 { get; set; }

    // ── PORODILJSKO BOLOVANJE (1 - 9) ──
    [Column(TypeName = "decimal(14,2)")] public decimal Por1 { get; set; }
    [Column(TypeName = "decimal(14,2)")] public decimal Por2 { get; set; }
    [Column(TypeName = "decimal(14,2)")] public decimal Por3 { get; set; }
    [Column(TypeName = "decimal(14,2)")] public decimal Por4 { get; set; }
    [Column(TypeName = "decimal(14,2)")] public decimal Por5 { get; set; }
    [Column(TypeName = "decimal(14,2)")] public decimal Por6 { get; set; }
    [Column(TypeName = "decimal(14,2)")] public decimal Por7 { get; set; }
    [Column(TypeName = "decimal(14,2)")] public decimal Por8 { get; set; }
    [Column(TypeName = "decimal(14,2)")] public decimal Por9 { get; set; }

    // Navigacija
    public Radnik Radnik { get; set; } = null!;
}
