using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ERPiZaradeData;

/// <summary>
/// Koristi se isključivo od strane "dotnet ef" alata pri generisanju migracija.
/// Pokazuje na privremenu bazu jer se šema izvodi iz modela, ne iz podataka.
/// </summary>
public class PlataDbContextFactory : IDesignTimeDbContextFactory<PlataDbContext>
{
    public PlataDbContext CreateDbContext(string[] args)
    {
        var putanja = Path.Combine(Path.GetTempPath(), "plata_designtime.db");

        var optionsBuilder = new DbContextOptionsBuilder<PlataDbContext>();
        optionsBuilder.UseSqlite($"Data Source={putanja}");

        return new PlataDbContext(optionsBuilder.Options);
    }
}
