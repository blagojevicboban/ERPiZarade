using System;
using System.Collections.Generic;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ERPiZaradeApp.Services;
using ERPiZaradeData.Models;

namespace ERPiZaradeApp.Views.Bolovanja;

/// <summary>
/// Obrazac <b>OZ-10</b> — spisak obračunatih i isplaćenih naknada zarada, koji se filijali
/// RFZO predaje u dva primerka uz zahtev za refundaciju.
///
/// Kolone i njihov redosled prate obrazac sa sajta Fonda, uključujući i to da su na njemu
/// numerisane od nule. Formule iz zaglavlja obrasca — bruto naknada = 15 + 17 + 18 i
/// za isplatu = 15 + 16 + 17 + 18 — drži <see cref="Oz10Red"/>, pa ovde nema računanja.
///
/// Štampa se položeno: dvadeset kolona na uspravnu stranu ne staje čitljivo.
/// </summary>
public static class Oz10Document
{
    /// <summary>
    /// Osnovi redom kojim stoje u kolonama 6–13. Naslov je samo osnov, bez ponovljenog
    /// „број дана због" — ono stoji jednom, iznad grupe kolona, isto kao na obrascu.
    /// </summary>
    private static readonly (OsnovSprecenosti Osnov, string Naslov)[] Osnovi =
    [
        (OsnovSprecenosti.Bolest,                  "болести"),
        (OsnovSprecenosti.PovredaNaRadu,           "повреде на раду"),
        (OsnovSprecenosti.ProfesionalnaBolest,     "проф. болести"),
        (OsnovSprecenosti.NegaClanaPorodice,       "неге члана порoдице 65%"),
        (OsnovSprecenosti.NegaClanaPorodiceClan78, "неге члана порoдице чл. 78/3"),
        (OsnovSprecenosti.IzolacijaIPracenje,      "изолације и праћења"),
        (OsnovSprecenosti.DavalacTkivaIOrgana,     "даваоца ткива и органа"),
        (OsnovSprecenosti.OdrzavanjeTrudnoce,      "одржавања трудноће")
    ];

    public static void Sacuvaj(Oz10Spisak spisak, Firma? firma, string putanja)
        => Document.Create(c => c.Page(page => Stranica(page, spisak, firma))).GeneratePdf(putanja);

    private static void Stranica(PageDescriptor page, Oz10Spisak spisak, Firma? firma)
    {
        page.Size(PageSizes.A4.Landscape());
        page.Margin(1.0f, Unit.Centimetre);
        page.PageColor(Colors.White);
        page.DefaultTextStyle(x => x.FontSize(7).FontFamily("Calibri"));

        page.Header().Column(zaglavlje =>
        {
            zaglavlje.Item().Row(red =>
            {
                red.RelativeItem(3).Column(c =>
                {
                    c.Item().Text(firma?.Naziv ?? "").FontSize(9).Bold();
                    c.Item().Text("(назив послодавца)").FontSize(6.5f).FontColor(Colors.Grey.Darken2);
                    c.Item().PaddingTop(3).Text($"Седиште: {firma?.Adresa} {firma?.Grad}".Trim()).FontSize(7.5f);
                    c.Item().Text($"Посебан рачун послодавца број: {firma?.PosebanRacun}").FontSize(7.5f);
                    c.Item().Text($"Шифра делатности: {firma?.SifraDelatnosti}     ПИБ: {firma?.Pib}").FontSize(7.5f);
                });

                red.RelativeItem(2).AlignCenter().Column(c =>
                {
                    c.Item().Text("СПИСАК ОБРАЧУНАТИХ – ИСПЛАЋЕНИХ НАКНАДА ЗАРАДА").FontSize(10).Bold();
                    c.Item().PaddingTop(2).Text($"за {spisak.Mesec:D2}/{spisak.Godina}").FontSize(9).Bold();
                });

                red.RelativeItem(1).AlignRight().Column(c =>
                {
                    c.Item().Text("Образац ОЗ-10").FontSize(8).Bold();
                    c.Item().PaddingTop(3).Text("Број: ____________").FontSize(7.5f);
                    c.Item().Text($"Датум: {DateTime.Today:dd.MM.yyyy}.").FontSize(7.5f);
                });
            });

            zaglavlje.Item().PaddingTop(6);
        });

        page.Content().Column(sadrzaj =>
        {
            sadrzaj.Item().PaddingBottom(2).Text("Број дана за које је исплаћена накнада због:")
                .FontSize(6.5f).Bold().FontColor(Colors.Grey.Darken3);

            sadrzaj.Item().Table(tabela =>
            {
                tabela.ColumnsDefinition(kolone =>
                {
                    kolone.ConstantColumn(18);    // 0  redni broj
                    kolone.RelativeColumn(3.4f);  // 1  prezime i ime
                    kolone.ConstantColumn(16);    // 2  pol
                    kolone.ConstantColumn(34);    // 3  prva isplata
                    kolone.ConstantColumn(42);    // 4  od
                    kolone.ConstantColumn(42);    // 5  do
                    for (int i = 0; i < Osnovi.Length; i++) kolone.ConstantColumn(36);   // 6–13 dani
                    kolone.RelativeColumn(1.6f);  // 14 bruto naknada
                    kolone.RelativeColumn(1.4f);  // 15 doprinosi iz naknade
                    kolone.RelativeColumn(1.4f);  // 16 doprinosi na naknadu
                    kolone.RelativeColumn(1.2f);  // 17 porez
                    kolone.RelativeColumn(1.5f);  // 18 neto naknada
                    kolone.RelativeColumn(1.6f);  // 19 za isplatu
                });

                tabela.Header(z =>
                {
                    Naslov(z.Cell(), "Редни број");
                    Naslov(z.Cell(), "ПРЕЗИМЕ И ИМЕ ОСИГУРАНИКА");
                    Naslov(z.Cell(), "Пол осиг.");
                    Naslov(z.Cell(), "Да ли је прва исплата *");
                    Naslov(z.Cell(), "Накнада обрачуната за време — од");
                    Naslov(z.Cell(), "до");

                    foreach (var (_, naslov) in Osnovi)
                        Naslov(z.Cell(), naslov);

                    Naslov(z.Cell(), "бруто накнада (15+17+18)");
                    Naslov(z.Cell(), "Доприноси из накнаде");
                    Naslov(z.Cell(), "Доприноси на накнаду");
                    Naslov(z.Cell(), "порез");
                    Naslov(z.Cell(), "нето накнада");
                    Naslov(z.Cell(), "за исплату (15+16+17+18)");

                    // Numeracija kolona sa obrasca; na nju se pozivaju formule u naslovima.
                    for (int broj = 0; broj <= 19; broj++) Broj(z.Cell(), broj);
                });

                foreach (var red in spisak.Redovi)
                {
                    Celija(tabela.Cell(), red.RedniBroj.ToString(), sredina: true);
                    Celija(tabela.Cell(), red.Radnik.ImeIPrezime);
                    Celija(tabela.Cell(), red.Pol, sredina: true);
                    Celija(tabela.Cell(), red.PrvaIsplataStr, sredina: true);
                    Celija(tabela.Cell(), red.DatumOd.ToString("dd.MM.yyyy"), sredina: true);
                    Celija(tabela.Cell(), red.DatumDo.ToString("dd.MM.yyyy"), sredina: true);

                    foreach (var (osnov, _) in Osnovi)
                    {
                        int dani = red.DaniZa(osnov);
                        Celija(tabela.Cell(), dani > 0 ? dani.ToString() : "", sredina: true);
                    }

                    Celija(tabela.Cell(), red.BrutoNaknada.ToString("N2"), desno: true);
                    Celija(tabela.Cell(), red.DoprinosiIzNaknade.ToString("N2"), desno: true);
                    Celija(tabela.Cell(), red.DoprinosiNaNaknadu.ToString("N2"), desno: true);
                    Celija(tabela.Cell(), red.Porez.ToString("N2"), desno: true);
                    Celija(tabela.Cell(), red.NetoNaknada.ToString("N2"), desno: true);
                    Celija(tabela.Cell(), red.ZaIsplatu.ToString("N2"), desno: true);
                }

                Zbir(tabela.Cell().ColumnSpan((uint)(6 + Osnovi.Length)), "УКУПНО");
                Zbir(tabela.Cell(), spisak.UkupnoBruto.ToString("N2"), desno: true);
                Zbir(tabela.Cell(), spisak.UkupnoDoprinosiIz.ToString("N2"), desno: true);
                Zbir(tabela.Cell(), spisak.UkupnoDoprinosiNa.ToString("N2"), desno: true);
                Zbir(tabela.Cell(), spisak.UkupnoPorez.ToString("N2"), desno: true);
                Zbir(tabela.Cell(), spisak.UkupnoNeto.ToString("N2"), desno: true);
                Zbir(tabela.Cell(), spisak.UkupnoZaIsplatu.ToString("N2"), desno: true);
            });

            sadrzaj.Item().PaddingTop(4).Text(
                "* Ако се ради о првој исплати из средстава фонда уписује се „да“, у осталим случајевима уписује се „-“.")
                .FontSize(6.5f).FontColor(Colors.Grey.Darken2);

            sadrzaj.Item().PaddingTop(16).Row(red =>
            {
                red.RelativeItem().Element(c => Potpis(c, "Обрачун извршио", "(Презиме и име)"));
                red.ConstantItem(16);
                red.RelativeItem().Element(c => Potpis(c, "Финансијски руководилац", "(Презиме и име)"));
                red.ConstantItem(16);
                red.RelativeItem().Element(c => Potpis(c,
                    "Право, висину и контролу обрачуна накнаде зараде извршио", "(Презиме и име)"));
            });

            sadrzaj.Item().PaddingTop(10).Row(red =>
            {
                red.RelativeItem().Text("Републички фонд – филијала ____________________     Број: ____________     Датум: ____________")
                    .FontSize(7);
                red.ConstantItem(80).AlignRight().Text("(М.П.)").FontSize(7);
            });

            sadrzaj.Item().PaddingTop(8).Text(
                "Напомена: печат не стављају привредна друштва, односно предузетници у складу са чланом 25. став 3. " +
                "Закона о привредним друштвима.").FontSize(6.5f).FontColor(Colors.Grey.Darken2);
        });

        page.Footer().AlignCenter().Text(t =>
        {
            t.Span("Страна ").FontSize(7).FontColor(Colors.Grey.Darken1);
            t.CurrentPageNumber().FontSize(7).FontColor(Colors.Grey.Darken1);
            t.Span(" од ").FontSize(7).FontColor(Colors.Grey.Darken1);
            t.TotalPages().FontSize(7).FontColor(Colors.Grey.Darken1);
        });
    }

    private static void Potpis(IContainer container, string naslov, string ispod)
        => container.Column(c =>
        {
            c.Item().AlignCenter().Text(naslov).FontSize(7);
            c.Item().PaddingTop(18).LineHorizontal(0.5f).LineColor(Colors.Grey.Medium);
            c.Item().AlignCenter().Text(ispod).FontSize(6.5f).FontColor(Colors.Grey.Darken2);
        });

    private static void Naslov(IContainer celija, string tekst)
        => celija.Background(Colors.Grey.Lighten3).Border(0.5f).BorderColor(Colors.Grey.Medium).Padding(2)
            .AlignCenter().Text(tekst).FontSize(5.5f).Bold();

    private static void Broj(IContainer celija, int broj)
        => celija.Background(Colors.Grey.Lighten4).Border(0.5f).BorderColor(Colors.Grey.Medium).Padding(1)
            .AlignCenter().Text(broj.ToString()).FontSize(5.5f).FontColor(Colors.Grey.Darken2);

    private static void Celija(IContainer celija, string tekst, bool desno = false, bool sredina = false)
    {
        var sadrzaj = celija.Border(0.5f).BorderColor(Colors.Grey.Medium).Padding(2);
        var poravnat = desno ? sadrzaj.AlignRight() : sredina ? sadrzaj.AlignCenter() : sadrzaj;
        poravnat.Text(tekst).FontSize(6.5f);
    }

    private static void Zbir(IContainer celija, string tekst, bool desno = false)
    {
        var sadrzaj = celija.Background(Colors.Grey.Lighten4).Border(0.5f).BorderColor(Colors.Grey.Medium).Padding(2);
        (desno ? sadrzaj.AlignRight() : sadrzaj).Text(tekst).FontSize(6.5f).Bold();
    }
}
