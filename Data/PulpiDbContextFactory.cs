using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace webapi_restful_backend_pulpi.Data;

/// <summary>
/// Permite ejecutar las migraciones de EF Core sin arrancar la API ni exigir
/// una clave JWT durante el diseño de la base de datos.
/// </summary>
public sealed class PulpiDbContextFactory : IDesignTimeDbContextFactory<PulpiDbContext>
{
    public PulpiDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Falta configurar ConnectionStrings:DefaultConnection en appsettings.json.");

        var options = new DbContextOptionsBuilder<PulpiDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new PulpiDbContext(options);
    }
}
