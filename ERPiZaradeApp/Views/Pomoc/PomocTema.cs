namespace ERPiZaradeApp.Views.Pomoc;

public class PomocTema
{
    public string Naslov { get; set; } = string.Empty;
    public string Sadrzaj { get; set; } = string.Empty;

    /// <summary>Ključ sekcije (npr. "Radnici", "Obracun") za kontekstualni F1 skok iz MainWindow-a. Null ako tema nema 1:1 sidebar stavku.</summary>
    public string? Kljuc { get; set; }
}
