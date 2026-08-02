using System.Collections.Generic;
using System.Linq;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ERPiZaradeApp.Services;
using ERPiZaradeData.Models;

namespace ERPiZaradeApp.Views.PppPo;

/// <summary>
/// Obrazac PPP-PO — potvrda o plaćenim porezima i doprinosima po odbitku, koja se uručuje
/// radniku do 31. januara za prethodnu godinu.
/// </summary>
public static class PppPoDocument
{
    public static void Sacuvaj(PppPoObrazac obrazac, Firma? firma, string putanja)
        => Document.Create(c => c.Page(page => Stranica(page, obrazac, firma))).GeneratePdf(putanja);

    /// <summary>Više potvrda u jednom dokumentu, svaka na svojoj strani.</summary>
    public static void Sacuvaj(IEnumerable<PppPoObrazac> obrasci, Firma? firma, string putanja)
        => Document.Create(c =>
        {
            foreach (var obrazac in obrasci) c.Page(page => Stranica(page, obrazac, firma));
        }).GeneratePdf(putanja);

    private static void Stranica(PageDescriptor page, PppPoObrazac obrazac, Firma? firma)
    {
        page.Size(PageSizes.A4);
        page.Margin(1.4f, Unit.Centimetre);
        page.PageColor(Colors.White);
        page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Calibri"));

        page.Header().Column(zaglavlje =>
        {
            zaglavlje.Item().AlignCenter().Text("ПОТВРДА О ПЛАЋЕНИМ ПОРЕЗИМА И ДОПРИНОСИМА ПО ОДБИТКУ")
                .FontSize(12).Bold();
            zaglavlje.Item().AlignCenter().Text("Образац ППП-ПО").FontSize(9).FontColor(Colors.Grey.Darken2);
            zaglavlje.Item().AlignCenter().PaddingTop(2)
                .Text($"за {obrazac.Godina}. годину").FontSize(10).Bold();
            zaglavlje.Item().PaddingTop(8).LineHorizontal(1).LineColor(Colors.Grey.Medium);
        });

        page.Content().PaddingTop(10).Column(sadrzaj =>
        {
            sadrzaj.Item().Row(red =>
            {
                red.RelativeItem().Element(c => Blok(c, "ИСПЛАТИЛАЦ ПРИХОДА",
                [
                    ("Назив", firma?.Naziv ?? ""),
                    ("ПИБ", firma?.Pib ?? ""),
                    ("Матични број", firma?.Mb ?? ""),
                    ("Адреса", $"{firma?.Adresa} {firma?.Grad}".Trim())
                ]));

                red.ConstantItem(12);

                red.RelativeItem().Element(c => Blok(c, "ПРИМАЛАЦ ПРИХОДА",
                [
                    ("Име и презиме", obrazac.Radnik.ImeIPrezime),
                    ("ЈМБГ", obrazac.Radnik.Jmbg),
                    ("Адреса", obrazac.Radnik.AdresaStanovanja),
                    ("Место", obrazac.Radnik.Mesto)
                ]));
            });

            sadrzaj.Item().PaddingTop(12).Text("ПОДАЦИ О ПРИХОДИМА").FontSize(9).Bold();

            sadrzaj.Item().PaddingTop(4).Table(tabela =>
            {
                tabela.ColumnsDefinition(kolone =>
                {
                    kolone.ConstantColumn(24);    // redni broj
                    kolone.ConstantColumn(72);    // šifra vrste prihoda
                    kolone.ConstantColumn(40);    // broj meseci
                    kolone.RelativeColumn();      // bruto
                    kolone.RelativeColumn();      // osnovica
                    kolone.RelativeColumn();      // porez
                    kolone.RelativeColumn();      // doprinosi
                });

                tabela.Header(z =>
                {
                    Naslov(z.Cell(), "Р.бр.");
                    Naslov(z.Cell(), "Шифра врсте прихода");
                    Naslov(z.Cell(), "Бр. месеци");
                    Naslov(z.Cell(), "Бруто приход", desno: true);
                    Naslov(z.Cell(), "Основица за порез", desno: true);
                    Naslov(z.Cell(), "Плаћени порез", desno: true);
                    Naslov(z.Cell(), "Плаћени доприноси", desno: true);
                });

                int redniBroj = 1;
                foreach (var red in obrazac.Redovi)
                {
                    Celija(tabela.Cell(), redniBroj++.ToString());
                    Celija(tabela.Cell(), red.Svp);
                    Celija(tabela.Cell(), red.Meseci.Count.ToString());
                    Celija(tabela.Cell(), red.BrutoPrihod.ToString("N2"), desno: true);
                    Celija(tabela.Cell(), red.PoreskaOsnovica.ToString("N2"), desno: true);
                    Celija(tabela.Cell(), red.Porez.ToString("N2"), desno: true);
                    Celija(tabela.Cell(), red.UkupnoDoprinosi.ToString("N2"), desno: true);
                }

                Zbir(tabela.Cell().ColumnSpan(3), "УКУПНО");
                Zbir(tabela.Cell(), obrazac.UkupnoBruto.ToString("N2"), desno: true);
                Zbir(tabela.Cell(), obrazac.UkupnoOsnovica.ToString("N2"), desno: true);
                Zbir(tabela.Cell(), obrazac.UkupnoPorez.ToString("N2"), desno: true);
                Zbir(tabela.Cell(), obrazac.UkupnoDoprinosi.ToString("N2"), desno: true);
            });

            sadrzaj.Item().PaddingTop(6).Text(
                "Доприноси су приказани у делу који пада на терет запосленог.")
                .FontSize(7.5f).FontColor(Colors.Grey.Darken2);

            sadrzaj.Item().PaddingTop(28).Row(red =>
            {
                red.RelativeItem().Column(c =>
                {
                    c.Item().Text($"Датум: ____.____.{obrazac.Godina + 1}.").FontSize(9);
                });

                red.ConstantItem(160);

                red.RelativeItem().Column(c =>
                {
                    c.Item().PaddingTop(14).LineHorizontal(0.5f).LineColor(Colors.Grey.Medium);
                    c.Item().PaddingTop(2).AlignCenter()
                        .Text("Потпис овлашћеног лица").FontSize(7.5f).FontColor(Colors.Grey.Darken2);
                });
            });
        });

        page.Footer().AlignCenter().Text(t =>
        {
            t.Span("Страна ").FontSize(8).FontColor(Colors.Grey.Darken1);
            t.CurrentPageNumber().FontSize(8).FontColor(Colors.Grey.Darken1);
            t.Span(" од ").FontSize(8).FontColor(Colors.Grey.Darken1);
            t.TotalPages().FontSize(8).FontColor(Colors.Grey.Darken1);
        });
    }

    private static void Blok(IContainer container, string naslov, (string Naziv, string Vrednost)[] stavke)
    {
        container.Border(0.5f).BorderColor(Colors.Grey.Medium).Padding(8).Column(c =>
        {
            c.Item().Text(naslov).FontSize(8).Bold().FontColor(Colors.Grey.Darken3);
            c.Item().PaddingTop(4);

            foreach (var (naziv, vrednost) in stavke)
            {
                c.Item().Row(r =>
                {
                    r.ConstantItem(78).Text($"{naziv}:").FontSize(8).FontColor(Colors.Grey.Darken2);
                    r.RelativeItem().Text(vrednost).FontSize(8);
                });
            }
        });
    }

    private static void Naslov(IContainer celija, string tekst, bool desno = false)
    {
        var sadrzaj = celija.Background(Colors.Grey.Lighten3).Border(0.5f).BorderColor(Colors.Grey.Medium).Padding(4);
        (desno ? sadrzaj.AlignRight() : sadrzaj).Text(tekst).FontSize(7.5f).Bold();
    }

    private static void Celija(IContainer celija, string tekst, bool desno = false)
    {
        var sadrzaj = celija.Border(0.5f).BorderColor(Colors.Grey.Medium).Padding(4);
        (desno ? sadrzaj.AlignRight() : sadrzaj).Text(tekst).FontSize(8);
    }

    private static void Zbir(IContainer celija, string tekst, bool desno = false)
    {
        var sadrzaj = celija.Background(Colors.Grey.Lighten4).Border(0.5f).BorderColor(Colors.Grey.Medium).Padding(4);
        (desno ? sadrzaj.AlignRight() : sadrzaj).Text(tekst).FontSize(8).Bold();
    }
}
