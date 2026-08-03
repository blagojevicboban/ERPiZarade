using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ERPiZaradeApp.Services;
using ERPiZaradeData.Models;

namespace ERPiZaradeApp.Views.Bolovanja;

/// <summary>
/// Obrazac <b>OZ-7</b> — potvrda o ostvarenoj zaradi za utvrđivanje osnova za obračun
/// naknade zarade, koja se uz zahtev za refundaciju predaje filijali RFZO.
///
/// Raspored i tekst prate obrazac sa sajta Fonda. Rubrike koje se po obrascu popunjavaju
/// rukom — prethodni staž osiguranja, mesto i datum, potpis — ostaju prazne i ovde: program
/// ih nema, a popunjena rubrika koju niko nije proverio gore je od prazne.
/// </summary>
public static class Oz7Document
{
    public static void Sacuvaj(Oz7Obrazac obrazac, Firma? firma, string putanja)
        => Document.Create(c => c.Page(page => Stranica(page, obrazac, firma))).GeneratePdf(putanja);

    private static void Stranica(PageDescriptor page, Oz7Obrazac obrazac, Firma? firma)
    {
        page.Size(PageSizes.A4);
        page.Margin(1.4f, Unit.Centimetre);
        page.PageColor(Colors.White);
        page.DefaultTextStyle(x => x.FontSize(8.5f).FontFamily("Calibri"));

        page.Header().Column(zaglavlje =>
        {
            zaglavlje.Item().AlignRight().Text("Образац ОЗ-7").FontSize(9).Bold();

            zaglavlje.Item().PaddingTop(4).Element(c => Podatak(c, "Назив и седиште послодавца",
                $"{firma?.Naziv} {firma?.Adresa} {firma?.Grad}".Trim()));
            zaglavlje.Item().Element(c => Podatak(c, "ПИБ/ЈМБГ", firma?.Pib ?? ""));
            zaglavlje.Item().Element(c => Podatak(c, "Посебан текући рачун", firma?.PosebanRacun ?? ""));
            zaglavlje.Item().Element(c => Podatak(c, "Подрачун пословне јединице", firma?.PodracunPoslovneJedinice ?? ""));
            zaglavlje.Item().Element(c => Podatak(c, "Број телефона", firma?.Telefon ?? ""));

            zaglavlje.Item().PaddingTop(10).AlignCenter().Text("П О Т В Р Д А").FontSize(12).Bold();
            zaglavlje.Item().AlignCenter().Text("О ОСТВАРЕНОЈ ЗАРАДИ ЗА УТВРЂИВАЊЕ").FontSize(10).Bold();
            zaglavlje.Item().AlignCenter().Text("ОСНОВА ЗА ОБРАЧУН НАКНАДЕ ЗАРАДЕ").FontSize(10).Bold();
        });

        page.Content().PaddingTop(10).Column(sadrzaj =>
        {
            sadrzaj.Item().Row(red =>
            {
                red.RelativeItem(3).Element(c => Podatak(c, "Запослени/предузетник", obrazac.Radnik.ImeIPrezime));
                red.ConstantItem(12);
                red.RelativeItem(2).Element(c => Podatak(c, "ЛБО", obrazac.Radnik.Lbo, sirinaNaziva: 30));
            });

            sadrzaj.Item().PaddingTop(8).Text(
                "Остварио је зараду/накнаду зараде у 12 месеци који претходе месецу у коме је наступила " +
                "привремена спреченост за рад и то:").FontSize(8.5f);

            sadrzaj.Item().PaddingTop(4).Table(tabela =>
            {
                tabela.ColumnsDefinition(kolone =>
                {
                    kolone.ConstantColumn(70);   // 1 mesec i godina
                    kolone.RelativeColumn(2);    // 2 časovi
                    kolone.RelativeColumn(3);    // 3 neto
                    kolone.RelativeColumn(3);    // 4 bruto
                    kolone.RelativeColumn(2);    // 5 datum isplate
                });

                tabela.Header(z =>
                {
                    Naslov(z.Cell(), "Месец и година");
                    Naslov(z.Cell(), "Укупан број часова за које је запослени остварио зараду/накнаду зараде*");
                    Naslov(z.Cell(), "Износ остварене зараде/накнаде зараде за укупан број часова без пореза и доприноса** (нето)");
                    Naslov(z.Cell(), "Износ остварене зараде/накнаде зараде за укупан број часова са обрачунатим порезима и доприносима*** (бруто)");
                    Naslov(z.Cell(), "Датум исплате зараде/накнаде зараде****");
                });

                // Red sa brojevima kolona stoji i na obrascu — po njemu se pozivaju formule ispod.
                for (int i = 1; i <= 5; i++) Celija(tabela.Cell(), i.ToString(), sredina: true);

                foreach (var red in obrazac.Redovi)
                {
                    Celija(tabela.Cell(), red.PeriodStr);
                    Celija(tabela.Cell(), red.Casovi > 0 ? red.Casovi.ToString() : "", desno: true);
                    Celija(tabela.Cell(), red.Neto != 0 ? red.Neto.ToString("N2") : "", desno: true);
                    Celija(tabela.Cell(), red.Bruto != 0 ? red.Bruto.ToString("N2") : "", desno: true);
                    Celija(tabela.Cell(), red.DatumIsplate?.ToString("dd.MM.yyyy") ?? "", sredina: true);
                }

                Zbir(tabela.Cell(), "Укупно:");
                Zbir(tabela.Cell(), obrazac.UkupnoCasova.ToString(), desno: true);
                Zbir(tabela.Cell(), obrazac.UkupnoNeto.ToString("N2"), desno: true);
                Zbir(tabela.Cell(), obrazac.UkupnoBruto.ToString("N2"), desno: true);
                Zbir(tabela.Cell(), "");
            });

            sadrzaj.Item().PaddingTop(8).Element(c => Prosek(c,
                "Просечан износ остварене зараде/накнаде зараде по часу без пореза и доприноса:",
                "укупно колона 3 подељено са укупно колона 2", obrazac.ProsekNetoPoCasu));

            sadrzaj.Item().PaddingTop(4).Element(c => Prosek(c,
                "Просечан износ остварене зараде/накнаде зараде по часу са обрачунатим порезом и доприносима:",
                "укупно колона 4 подељен са укупно колона 2", obrazac.ProsekBrutoPoCasu));

            sadrzaj.Item().PaddingTop(10).Text(
                "Запослени/предузетник  има – нема (заокружити) претходни стаж осигурања у трајању од најмање " +
                "три месеца непрекидно или шест месеци са прекидима у последњих 18 месеци пре почетка коришћења " +
                "права из обавезног здравственог осигурања.").FontSize(8);

            sadrzaj.Item().PaddingTop(10).Column(napomene =>
            {
                Fusnota(napomene, "*", "Укупан број (свих) часова за које је запослени остварио зараду чине сви часови у току месеца " +
                    "за које је запосленом обрачуната и исплаћена зарада (часови проведени на раду, часови проведени у прековременом " +
                    "раду, рад на дан државног празника, часови за рад ноћу), односно накнада зараде због привремене спречености за " +
                    "рад, плаћеног одсуства, годишњег одмора, породиљског одсуства.");

                Fusnota(napomene, "**", "Износ остварене зараде је зарада без пореза и доприноса коју је запослени остварио у току " +
                    "месеца и сходно члану 87. став 2. Закона о здравственом осигурању чине је основна зарада, део зараде за радни " +
                    "учинак и увећана зарада. У зараду не улазе накнаде трошкова (превоз, службени пут, терен), отпремнина, јубиларна " +
                    "награда и солидарна помоћ.");

                Fusnota(napomene, "***", "Износ остварене зараде/накнаде зараде је нето зарада/накнада зараде увећана за порезе и " +
                    "доприносе (бруто зарада/накнада зараде).");

                Fusnota(napomene, "****", "Датум исплате је датум када је извршена последња (коначна) исплата зараде/накнаде зараде.");

                Fusnota(napomene, "1.", "За месеце у којима запослени није био у радном односу у колону 3 уписује се износ минималне " +
                    "зараде за тај месец.");
            });

            sadrzaj.Item().PaddingTop(20).Row(red =>
            {
                red.RelativeItem().Column(c =>
                {
                    c.Item().Text("У   __________________").FontSize(8.5f);
                    c.Item().PaddingTop(10).Text("Дана   ________________").FontSize(8.5f);
                });

                red.ConstantItem(40);

                red.RelativeItem().AlignCenter().Text("М.П.").FontSize(8.5f);

                red.ConstantItem(40);

                red.RelativeItem().Column(c =>
                {
                    c.Item().AlignCenter().Text("ПОТПИС ОВЛАШЋЕНОГ ЛИЦА").FontSize(8);
                    c.Item().AlignCenter().Text("КОД ПОСЛОДАВЦА/ПРЕДУЗЕТНИКА").FontSize(8);
                    c.Item().PaddingTop(16).LineHorizontal(0.5f).LineColor(Colors.Grey.Medium);
                });
            });

            sadrzaj.Item().PaddingTop(10).Text(
                "НАПОМЕНА: Печат не стављају привредна друштва, односно предузетници у складу са чланом 25. став 3. " +
                "Закона о привредним друштвима.").FontSize(7).FontColor(Colors.Grey.Darken2);
        });
    }

    private static void Podatak(IContainer container, string naziv, string vrednost, float sirinaNaziva = 150)
        => container.Row(r =>
        {
            r.ConstantItem(sirinaNaziva).Text($"{naziv}:").FontSize(8).FontColor(Colors.Grey.Darken3);
            r.RelativeItem().BorderBottom(0.5f).BorderColor(Colors.Grey.Medium).Text(vrednost).FontSize(8.5f);
        });

    private static void Prosek(IContainer container, string naslov, string formula, decimal vrednost)
        => container.Row(r =>
        {
            r.RelativeItem().Column(c =>
            {
                c.Item().Text(naslov).FontSize(8.5f);
                c.Item().Text(formula).FontSize(7).FontColor(Colors.Grey.Darken2);
            });

            r.ConstantItem(110).AlignMiddle().Border(0.5f).BorderColor(Colors.Grey.Medium).Padding(4)
                .AlignRight().Text(vrednost > 0 ? vrednost.ToString("N4") : "").FontSize(9).Bold();
        });

    private static void Fusnota(ColumnDescriptor kolona, string oznaka, string tekst)
        => kolona.Item().PaddingTop(3).Row(r =>
        {
            r.ConstantItem(20).Text(oznaka).FontSize(7).Bold();
            r.RelativeItem().Text(tekst).FontSize(7).FontColor(Colors.Grey.Darken3);
        });

    private static void Naslov(IContainer celija, string tekst)
        => celija.Background(Colors.Grey.Lighten3).Border(0.5f).BorderColor(Colors.Grey.Medium).Padding(3)
            .AlignCenter().Text(tekst).FontSize(6.5f).Bold();

    private static void Celija(IContainer celija, string tekst, bool desno = false, bool sredina = false)
    {
        var sadrzaj = celija.Border(0.5f).BorderColor(Colors.Grey.Medium).Padding(3);
        var poravnat = desno ? sadrzaj.AlignRight() : sredina ? sadrzaj.AlignCenter() : sadrzaj;
        poravnat.Text(tekst).FontSize(8);
    }

    private static void Zbir(IContainer celija, string tekst, bool desno = false)
    {
        var sadrzaj = celija.Background(Colors.Grey.Lighten4).Border(0.5f).BorderColor(Colors.Grey.Medium).Padding(3);
        (desno ? sadrzaj.AlignRight() : sadrzaj).Text(tekst).FontSize(8).Bold();
    }
}
