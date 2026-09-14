using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Ludeka.Infrastructure.Data;

/// <summary>
/// Fábrica en tiempo de diseño para Entity Framework Core CLI (dotnet ef).
/// Permite generar migraciones oficiales e idempotentes para PostgreSQL / Supabase
/// sin requerir la ejecución del servidor web completo.
/// </summary>
public class LudekaDbContextFactory : IDesignTimeDbContextFactory<LudekaDbContext>
{
    public LudekaDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<LudekaDbContext>();
        
        // Cadena ficticia para scaffolding en tiempo de diseño (no se conecta a la red durante la generación de código)
        optionsBuilder.UseNpgsql("Host=db.supabase.co;Port=5432;Database=postgres;Username=postgres;Password=design_time_scaffolding");
        
        return new LudekaDbContext(optionsBuilder.Options);
    }
}
