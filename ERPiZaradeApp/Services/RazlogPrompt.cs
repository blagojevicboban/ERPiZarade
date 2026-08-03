using System;
using System.Windows;
using System.Windows.Controls;

namespace ERPiZaradeApp.Services;

/// <summary>
/// Traženje razloga za radnju koja se upisuje u revizioni trag.
///
/// Odvojeno od <see cref="StornoService"/> iz istog razloga kao <see cref="PreFlightPrompt"/>
/// od <see cref="PreFlightService"/> — servis ostaje bez veze sa UI-jem i proverljiv testom.
/// </summary>
public static class RazlogPrompt
{
    /// <summary>
    /// Prikazuje prozor za unos razloga. Vraća <c>null</c> kad korisnik odustane ili
    /// ostavi prazno — prazan razlog se namerno tretira kao odustajanje, jer bi zapis
    /// bez njega bio isto što i zapisa nema.
    /// </summary>
    public static string? Trazi(Window? vlasnik, string naslov, string pitanje)
    {
        var dijalog = new Window
        {
            Title = naslov,
            Width = 460,
            Height = 220,
            WindowStartupLocation = vlasnik != null
                ? WindowStartupLocation.CenterOwner
                : WindowStartupLocation.CenterScreen,
            Owner = vlasnik,
            ResizeMode = ResizeMode.NoResize,
            Background = System.Windows.Media.Brushes.White,
            Style = null
        };

        var stack = new StackPanel { Margin = new Thickness(16) };

        stack.Children.Add(new TextBlock
        {
            Text = pitanje,
            Margin = new Thickness(0, 0, 0, 10),
            TextWrapping = TextWrapping.Wrap,
            FontWeight = FontWeights.SemiBold
        });

        var unos = new TextBox
        {
            Height = 64,
            MaxLength = 200,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Margin = new Thickness(0, 0, 0, 14)
        };
        stack.Children.Add(unos);

        var dugmad = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        var btnOk = new Button
        {
            Content = "Potvrdi",
            Width = 84,
            Height = 26,
            IsDefault = true,
            Margin = new Thickness(0, 0, 8, 0)
        };
        btnOk.Click += (_, _) => { dijalog.DialogResult = true; dijalog.Close(); };

        var btnOtkazi = new Button
        {
            Content = "Otkaži",
            Width = 84,
            Height = 26,
            IsCancel = true
        };
        btnOtkazi.Click += (_, _) => { dijalog.DialogResult = false; dijalog.Close(); };

        dugmad.Children.Add(btnOk);
        dugmad.Children.Add(btnOtkazi);
        stack.Children.Add(dugmad);

        dijalog.Content = stack;
        unos.Loaded += (_, _) => unos.Focus();

        if (dijalog.ShowDialog() != true) return null;

        string razlog = unos.Text.Trim();
        return razlog.Length == 0 ? null : razlog;
    }
}
