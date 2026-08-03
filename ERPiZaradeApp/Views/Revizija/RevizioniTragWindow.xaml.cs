using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using ERPiZaradeData;
using ERPiZaradeData.Models;
using ERPiZaradeApp.Services;

namespace ERPiZaradeApp.Views.Revizija;

/// <summary>
/// Prikaz revizionog traga i arhive verzija za jedan obračunski period.
///
/// Trag se upisuje od Faze 0, ali se do sada nije mogao pogledati — a zapis koji niko ne
/// vidi ne odgovara ni na jedno pitanje koje se pri kontroli postavi.
/// </summary>
public partial class RevizioniTragWindow : Window
{
    /// <summary>Red revizionog traga sa čitljivim opisom radnje.</summary>
    public sealed class RedTraga
    {
        public DateTime Vreme { get; init; }
        public string AkcijaOpis { get; init; } = "";
        public int? BrojRadnika { get; init; }
        public string? ImeRadnika { get; init; }
        public string? KorisnickoIme { get; init; }
        public string? Detalji { get; init; }
    }

    public RevizioniTragWindow(int godina, int mesec)
    {
        InitializeComponent();

        string[] meseci = {
            "Januar", "Februar", "Mart", "April", "Maj", "Jun",
            "Jul", "Avgust", "Septembar", "Oktobar", "Novembar", "Decembar"
        };
        string period = mesec is >= 1 and <= 12 ? $"{meseci[mesec - 1]} {godina}" : $"{mesec:D2}/{godina}";
        Naslov.Text = $"🕓 Revizioni trag — {period}";

        Ucitaj(godina, mesec);
    }

    private void Ucitaj(int godina, int mesec)
    {
        try
        {
            using var db = PlataDbContext.Create(AppConfig.DbPath);

            var trag = db.ObracunAuditi
                .AsNoTracking()
                .Where(a => a.Godina == godina && a.Mesec == mesec)
                .OrderByDescending(a => a.Vreme)
                .ToList()
                .Select(a => new RedTraga
                {
                    Vreme = a.Vreme,
                    AkcijaOpis = AuditService.OpisAkcije(a.Akcija),
                    BrojRadnika = a.BrojRadnika,
                    ImeRadnika = a.ImeRadnika,
                    KorisnickoIme = a.KorisnickoIme,
                    Detalji = a.Detalji
                })
                .ToList();

            TragGrid.ItemsSource = trag;

            var verzije = db.ObracunVerzije
                .AsNoTracking()
                .Where(v => v.Godina == godina && v.Mesec == mesec)
                .OrderByDescending(v => v.Vreme)
                .ThenBy(v => v.BrojRadnika)
                .ToList();

            VerzijeGrid.ItemsSource = verzije;

            StatusText.Text = $"Radnji: {trag.Count} · arhiviranih verzija: {verzije.Count}.";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Greška pri učitavanju revizionog traga: {ex.Message}";
        }
    }

    private void BtnZatvori_Click(object sender, RoutedEventArgs e) => Close();
}
