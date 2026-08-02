using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERPiZaradeData.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Banke",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Godina = table.Column<int>(type: "INTEGER", nullable: false),
                    Mesec = table.Column<int>(type: "INTEGER", nullable: false),
                    Sifra = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    Naziv = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    ZiroRacun = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Banke", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Doprinosi",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Godina = table.Column<int>(type: "INTEGER", nullable: false),
                    Mesec = table.Column<int>(type: "INTEGER", nullable: false),
                    RedniBroj = table.Column<int>(type: "INTEGER", nullable: false),
                    Naziv = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    ProcRadn = table.Column<decimal>(type: "decimal(6,3)", nullable: false),
                    ProcPosl = table.Column<decimal>(type: "decimal(6,3)", nullable: false),
                    B60ProcR = table.Column<decimal>(type: "decimal(6,3)", nullable: false),
                    B60ProcP = table.Column<decimal>(type: "decimal(6,3)", nullable: false),
                    Bp60ProcP = table.Column<decimal>(type: "decimal(6,2)", nullable: false),
                    Bp60FProcP = table.Column<decimal>(type: "decimal(6,2)", nullable: false),
                    PorProcP = table.Column<decimal>(type: "decimal(6,2)", nullable: false),
                    NepProcP = table.Column<decimal>(type: "decimal(6,2)", nullable: false),
                    InvProcP = table.Column<decimal>(type: "decimal(6,2)", nullable: false),
                    Svrha1 = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    Svrha2 = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    Primalac1 = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    Primalac2 = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    ZiroRacun = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    ZiroRacP = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    PozivNaB = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    PozivNa2 = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    SifPlac = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    SifPlacP = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    NajnizaOsnovica = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    NajvisaOsnovica = table.Column<decimal>(type: "decimal(14,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Doprinosi", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Firme",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Naziv = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Adresa = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Grad = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Pib = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    Mb = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    BankovniRacun = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    SifraPlacanja = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Telefon = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Email = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Napomena = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Firme", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Kategorije",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Sifra = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    Naziv = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    Koeficijent = table.Column<decimal>(type: "decimal(8,4)", nullable: false),
                    StopaPio = table.Column<decimal>(type: "decimal(6,4)", nullable: false),
                    StopaZdravstvo = table.Column<decimal>(type: "decimal(6,4)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Kategorije", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Korisnici",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ImePrezime = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    KorisnickoIme = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    LozinkaHash = table.Column<string>(type: "TEXT", nullable: false),
                    Uloga = table.Column<int>(type: "INTEGER", nullable: false),
                    JeAktivan = table.Column<bool>(type: "INTEGER", nullable: false),
                    PoslednjaPrijava = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Korisnici", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Normativi",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Sifra = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Naziv = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    VrednostBoda = table.Column<decimal>(type: "decimal(10,4)", nullable: false),
                    Tip = table.Column<char>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Normativi", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PlatniRazredi",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    R1 = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    R2 = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    R3 = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    R4 = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    R5 = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    R6 = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    R7 = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    R8 = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    R9 = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    P1 = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    P2 = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    P3 = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    P4 = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    P5 = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    P6 = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    P7 = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    P8 = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    P9 = table.Column<decimal>(type: "decimal(14,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatniRazredi", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PoreskeStope",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RedniBroj = table.Column<int>(type: "INTEGER", nullable: false),
                    GranjaOd = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    GranicaDo = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    Stopa = table.Column<decimal>(type: "decimal(6,4)", nullable: false),
                    FiksniIznos = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    GodisnjuVazenja = table.Column<int>(type: "INTEGER", nullable: false),
                    MesecVazenja = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PoreskeStope", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Porezi",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Godina = table.Column<int>(type: "INTEGER", nullable: false),
                    Mesec = table.Column<int>(type: "INTEGER", nullable: false),
                    RedniBroj = table.Column<int>(type: "INTEGER", nullable: false),
                    Zarada = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    AkPorez = table.Column<decimal>(type: "decimal(6,2)", nullable: false),
                    AkPorez2 = table.Column<decimal>(type: "decimal(6,2)", nullable: false),
                    AkPorez3 = table.Column<decimal>(type: "decimal(6,2)", nullable: false),
                    AkPorez4 = table.Column<decimal>(type: "decimal(6,2)", nullable: false),
                    Prvast = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    Drugast = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    Trecast = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    LinPorez3 = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    SifPlac1 = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    ZiroR1 = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    PozivNa1 = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    PozivNa3 = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Svrha1 = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    Svrha2 = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    Primalac1 = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    Primalac2 = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    SifPlac2 = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    ZiroR2 = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    PozivNa2 = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    PozivNa4 = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    PosPorez = table.Column<decimal>(type: "decimal(6,2)", nullable: false),
                    Svrha3 = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    Svrha4 = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    Primalac3 = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    Primalac4 = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    ProcDrzav = table.Column<decimal>(type: "decimal(6,2)", nullable: false),
                    ProcNocni = table.Column<decimal>(type: "decimal(6,2)", nullable: false),
                    ProcPreko = table.Column<decimal>(type: "decimal(6,2)", nullable: false),
                    ProcMinul = table.Column<decimal>(type: "decimal(6,2)", nullable: false),
                    ProcNedel = table.Column<decimal>(type: "decimal(6,2)", nullable: false),
                    ProcBolov = table.Column<decimal>(type: "decimal(6,2)", nullable: false),
                    ProcPlac = table.Column<decimal>(type: "decimal(6,2)", nullable: false),
                    ProcPlZa = table.Column<decimal>(type: "decimal(6,2)", nullable: false),
                    ProcInval = table.Column<decimal>(type: "decimal(6,2)", nullable: false),
                    FondCasova = table.Column<int>(type: "INTEGER", nullable: false),
                    CasZaOb = table.Column<int>(type: "INTEGER", nullable: false),
                    VrBoda = table.Column<decimal>(type: "decimal(10,4)", nullable: false),
                    ProcIzdrz = table.Column<decimal>(type: "decimal(6,2)", nullable: false),
                    Akont = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    ProsBrut = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    TopliObrokCena = table.Column<decimal>(type: "decimal(14,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Porezi", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Radnici",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Godina = table.Column<int>(type: "INTEGER", nullable: false),
                    Mesec = table.Column<int>(type: "INTEGER", nullable: false),
                    BrojRadnika = table.Column<int>(type: "INTEGER", nullable: false),
                    ImeIPrezime = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    Jmbg = table.Column<string>(type: "TEXT", maxLength: 13, nullable: false),
                    MaticniBroj = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    DatumRodjenja = table.Column<DateTime>(type: "TEXT", nullable: true),
                    MestoRodjenja = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    AdresaStanovanja = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Mesto = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    SifraOpstine = table.Column<string>(type: "TEXT", maxLength: 3, nullable: false),
                    DatumZaposlenja = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DatumPrestanka = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Kategorija = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    Radno_Mesto = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    BrojRadneJedinice = table.Column<int>(type: "INTEGER", nullable: false),
                    MinuliRadGodine = table.Column<int>(type: "INTEGER", nullable: false),
                    Koeficijent = table.Column<decimal>(type: "decimal(10,4)", nullable: false),
                    Koeficijent1 = table.Column<decimal>(type: "decimal(10,4)", nullable: false),
                    OsnovnaPlata = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    StopaPio = table.Column<decimal>(type: "decimal(6,4)", nullable: false),
                    StopaZdravstvo = table.Column<decimal>(type: "decimal(6,4)", nullable: false),
                    StopaNezaposlenost = table.Column<decimal>(type: "decimal(6,4)", nullable: false),
                    BankovniRacun = table.Column<string>(type: "TEXT", maxLength: 25, nullable: false),
                    NazivBanke = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    Aktivan = table.Column<bool>(type: "INTEGER", nullable: false),
                    LicnoOslobodjenje = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    Operativni = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    DatumUnosa = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DatumIzmene = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Radnici", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DoprinosiPoslodavca",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RadnikId = table.Column<int>(type: "INTEGER", nullable: false),
                    Godina = table.Column<int>(type: "INTEGER", nullable: false),
                    Mesec = table.Column<int>(type: "INTEGER", nullable: false),
                    Zar1 = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    Zar2 = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    Zar3 = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    Zar4 = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    Zar5 = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    Zar6 = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    Zar7 = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    Zar8 = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    Zar9 = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    Bol1 = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    Bol2 = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    Bol3 = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    Bol4 = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    Bol5 = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    Bol6 = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    Bol7 = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    Bol8 = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    Bol9 = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    Nak1 = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    Nak2 = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    Nak3 = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    Nak4 = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    Nak5 = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    Nak6 = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    Nak7 = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    Nak8 = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    Nak9 = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    Nep1 = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    Nep2 = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    Nep3 = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    Nep4 = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    Nep5 = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    Nep6 = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    Nep7 = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    Nep8 = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    Nep9 = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    B60F1 = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    B60F2 = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    B60F3 = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    B60F4 = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    B60F5 = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    B60F6 = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    B60F7 = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    B60F8 = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    B60F9 = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    B601 = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    B602 = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    B603 = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    B604 = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    B605 = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    B606 = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    B607 = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    B608 = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    B609 = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    Inv1 = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    Inv2 = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    Inv3 = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    Inv4 = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    Inv5 = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    Inv6 = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    Inv7 = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    Inv8 = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    Inv9 = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    Por1 = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    Por2 = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    Por3 = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    Por4 = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    Por5 = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    Por6 = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    Por7 = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    Por8 = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    Por9 = table.Column<decimal>(type: "decimal(14,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DoprinosiPoslodavca", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DoprinosiPoslodavca_Radnici_RadnikId",
                        column: x => x.RadnikId,
                        principalTable: "Radnici",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Krediti",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RadnikId = table.Column<int>(type: "INTEGER", nullable: false),
                    Opis = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    UkupanIznos = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    MesecnaRata = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    OstatakDuga = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    BrojRata = table.Column<int>(type: "INTEGER", nullable: false),
                    PlateneRate = table.Column<int>(type: "INTEGER", nullable: false),
                    DatumPocetka = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DatumZavrsetka = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Aktivan = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Krediti", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Krediti_Radnici_RadnikId",
                        column: x => x.RadnikId,
                        principalTable: "Radnici",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ObracuniPlata",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RadnikId = table.Column<int>(type: "INTEGER", nullable: false),
                    Godina = table.Column<int>(type: "INTEGER", nullable: false),
                    Mesec = table.Column<int>(type: "INTEGER", nullable: false),
                    Zakljucan = table.Column<bool>(type: "INTEGER", nullable: false),
                    BrutoZarada = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    BrutoBolovanje = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    BrutoNaknade = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    BrutoStimulacija = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    BrutoMinuliRad = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    NetoZar = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    NetoNerd = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    NetoGOd = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    NetoTo = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    NetoReg = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    Neto = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    NetoBol = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    NetoB100 = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    NetoPlac = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    NetoPlZ = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    NetoDrza = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    NetoNocni = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    NetoVezba = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    NetoPrek = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    NetoTer = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    KorDod = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    KorDod1 = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    Kumul = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    NetoNede = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    DoprinosPioRadnik = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    DoprinosZdravstvoRadnik = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    DoprinosNezaposlenostRadnik = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    DoprinosPioPoslodavac = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    DoprinosZdravstvoPoslodavac = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    DoprinosNezaposlenostPoslodavac = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    PorezNaDohodak = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    PoreskaOsnovica = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    LicniOdbitak = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    KreditObustava = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    Samodoprinosi = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    OstaliOdbici = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    NetoIsplata = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    RedovniSati = table.Column<int>(type: "INTEGER", nullable: false),
                    BolovanjeSati = table.Column<int>(type: "INTEGER", nullable: false),
                    PrekovremeneSati = table.Column<int>(type: "INTEGER", nullable: false),
                    GodisnjioOdmorSati = table.Column<int>(type: "INTEGER", nullable: false),
                    DrzavniPraznikSati = table.Column<int>(type: "INTEGER", nullable: false),
                    NocniSati = table.Column<int>(type: "INTEGER", nullable: false),
                    SmenskiSati = table.Column<int>(type: "INTEGER", nullable: false),
                    RadPraznikomSati = table.Column<int>(type: "INTEGER", nullable: false),
                    NocniRadPraznikomSati = table.Column<int>(type: "INTEGER", nullable: false),
                    PlacenoOdsustvoSati = table.Column<int>(type: "INTEGER", nullable: false),
                    Zakljucen = table.Column<bool>(type: "INTEGER", nullable: false),
                    DatumObracuna = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Prosek = table.Column<decimal>(type: "decimal(14,4)", nullable: false),
                    Napomena = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Koeficijent = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    MinuliRadGodine = table.Column<int>(type: "INTEGER", nullable: false),
                    Kategorija = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    BrojRadneJedinice = table.Column<int>(type: "INTEGER", nullable: false),
                    UkupnoRadnihSatiLegacy = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    FondSatiMesecni = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    CenaSataRedovan = table.Column<decimal>(type: "decimal(14,5)", nullable: false),
                    CenaSataMinuliRad = table.Column<decimal>(type: "decimal(14,5)", nullable: false),
                    DodaciLegacy = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    DodatakNaM1 = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    DodatakNaM2 = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    DodatakNaM3 = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    BrutoOsnovica = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    TopliObrokIznos = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    BrutoPioOsnovica = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    NetoNaknadeLegacy = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    Operativni = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Oznaka = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    NedeljaSati = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    BolovanjePreko60SatiLegacy = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    PorodiljskoOdsustvoSatiLegacy = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    PlacenoOdsustvoSatiLegacy = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    PlacenoZakonskiSatiLegacy = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    Bolovanje100SatiLegacy = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    MinimalnaPlataOsnovica = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    SifraSamodoprinosa1 = table.Column<int>(type: "INTEGER", nullable: false),
                    SifraSamodoprinosa2 = table.Column<int>(type: "INTEGER", nullable: false),
                    PosebanPorez = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    NetoPorez = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    NetoBezPoreza = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    Varijabila = table.Column<decimal>(type: "decimal(14,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ObracuniPlata", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ObracuniPlata_Radnici_RadnikId",
                        column: x => x.RadnikId,
                        principalTable: "Radnici",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RadniSati",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RadnikId = table.Column<int>(type: "INTEGER", nullable: false),
                    Godina = table.Column<int>(type: "INTEGER", nullable: false),
                    Mesec = table.Column<int>(type: "INTEGER", nullable: false),
                    RedovniSati = table.Column<int>(type: "INTEGER", nullable: false),
                    BolovanjeSati = table.Column<int>(type: "INTEGER", nullable: false),
                    PrekovremeneSati = table.Column<int>(type: "INTEGER", nullable: false),
                    GodisnjiOdmorSati = table.Column<int>(type: "INTEGER", nullable: false),
                    DrzavniPraznikSati = table.Column<int>(type: "INTEGER", nullable: false),
                    NocniSati = table.Column<int>(type: "INTEGER", nullable: false),
                    SmenskiSati = table.Column<int>(type: "INTEGER", nullable: false),
                    RadPraznikomSati = table.Column<int>(type: "INTEGER", nullable: false),
                    NocniRadPraznikomSati = table.Column<int>(type: "INTEGER", nullable: false),
                    PlacenoOdsustvoSati = table.Column<int>(type: "INTEGER", nullable: false),
                    Stimulacija = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    RadNedeljomSati = table.Column<int>(type: "INTEGER", nullable: false),
                    PlacenoZakonskiSati = table.Column<int>(type: "INTEGER", nullable: false),
                    BolovanjePreko60Sati = table.Column<int>(type: "INTEGER", nullable: false),
                    PorodiljskoOdsustvoSati = table.Column<int>(type: "INTEGER", nullable: false),
                    Bolovanje100Sati = table.Column<int>(type: "INTEGER", nullable: false),
                    TopliObrokDani = table.Column<int>(type: "INTEGER", nullable: false),
                    RegresIznos = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    Prosek = table.Column<decimal>(type: "decimal(14,4)", nullable: false),
                    Varijabila = table.Column<decimal>(type: "decimal(14,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RadniSati", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RadniSati_Radnici_RadnikId",
                        column: x => x.RadnikId,
                        principalTable: "Radnici",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Samodoprinosi",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RadnikId = table.Column<int>(type: "INTEGER", nullable: false),
                    Godina = table.Column<int>(type: "INTEGER", nullable: false),
                    Mesec = table.Column<int>(type: "INTEGER", nullable: false),
                    Iznos = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    Opis = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Samodoprinosi", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Samodoprinosi_Radnici_RadnikId",
                        column: x => x.RadnikId,
                        principalTable: "Radnici",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DoprinosiPoslodavca_RadnikId_Godina_Mesec",
                table: "DoprinosiPoslodavca",
                columns: new[] { "RadnikId", "Godina", "Mesec" });

            migrationBuilder.CreateIndex(
                name: "IX_Krediti_RadnikId",
                table: "Krediti",
                column: "RadnikId");

            migrationBuilder.CreateIndex(
                name: "IX_ObracuniPlata_RadnikId_Godina_Mesec",
                table: "ObracuniPlata",
                columns: new[] { "RadnikId", "Godina", "Mesec" });

            migrationBuilder.CreateIndex(
                name: "IX_Radnici_BrojRadnika",
                table: "Radnici",
                column: "BrojRadnika");

            migrationBuilder.CreateIndex(
                name: "IX_Radnici_BrojRadnika_Godina_Mesec",
                table: "Radnici",
                columns: new[] { "BrojRadnika", "Godina", "Mesec" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Radnici_Godina_Mesec",
                table: "Radnici",
                columns: new[] { "Godina", "Mesec" });

            migrationBuilder.CreateIndex(
                name: "IX_Radnici_Jmbg",
                table: "Radnici",
                column: "Jmbg");

            migrationBuilder.CreateIndex(
                name: "IX_RadniSati_RadnikId_Godina_Mesec",
                table: "RadniSati",
                columns: new[] { "RadnikId", "Godina", "Mesec" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Samodoprinosi_RadnikId",
                table: "Samodoprinosi",
                column: "RadnikId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Banke");

            migrationBuilder.DropTable(
                name: "Doprinosi");

            migrationBuilder.DropTable(
                name: "DoprinosiPoslodavca");

            migrationBuilder.DropTable(
                name: "Firme");

            migrationBuilder.DropTable(
                name: "Kategorije");

            migrationBuilder.DropTable(
                name: "Korisnici");

            migrationBuilder.DropTable(
                name: "Krediti");

            migrationBuilder.DropTable(
                name: "Normativi");

            migrationBuilder.DropTable(
                name: "ObracuniPlata");

            migrationBuilder.DropTable(
                name: "PlatniRazredi");

            migrationBuilder.DropTable(
                name: "PoreskeStope");

            migrationBuilder.DropTable(
                name: "Porezi");

            migrationBuilder.DropTable(
                name: "RadniSati");

            migrationBuilder.DropTable(
                name: "Samodoprinosi");

            migrationBuilder.DropTable(
                name: "Radnici");
        }
    }
}
