using System.IO;
using System.Linq;

using Artifacto.Database.DbContexts;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Artifacto.Database;

/// <summary>
/// Provides extension methods for registering database services with the dependency injection container.
/// </summary>
public static class DependencyInjectionExtensions
{
    /// <summary>
    /// Registers the SQLite implementation of ArtifactoDbContext with the dependency injection container.
    /// </summary>
    /// <param name="services">The service collection to add the DbContext to.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddArtifactoSqliteDbContext(this IServiceCollection services)
    {
        services.AddDbContext<ArtifactoDbContext, SqliteDbContext>();
        return services;
    }

    /// <summary>
    /// Creates the database if it doesn't exist and applies any pending migrations.
    /// For SQLite databases, this method also ensures the directory structure exists.
    /// </summary>
    /// <param name="app">The host application to retrieve services from.</param>
    /// <remarks>
    /// This method should be called during application startup to ensure the database
    /// is properly initialized before the application begins serving requests.
    /// For SQLite databases stored on disk, the method will create the necessary
    /// directory structure if it doesn't exist.
    /// </remarks>
    public static void CreateOrMigrateDatabase(this IHost app)
    {
        using IServiceScope scope = app.Services.CreateScope();

        using ArtifactoDbContext dbContext = scope.ServiceProvider.GetRequiredService<ArtifactoDbContext>();

        // Ensure the directory for the SQLite file exists
        System.Data.Common.DbConnection connection = dbContext.Database.GetDbConnection();
        string connectionString = connection.ConnectionString;
        SqliteConnectionStringBuilder sqliteBuilder = new(connectionString);
        if (sqliteBuilder.DataSource != ":memory:")
        {
            string? folder = Path.GetDirectoryName(sqliteBuilder.DataSource);
            if (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }
        }

        if (dbContext.Database.GetPendingMigrations().Any())
        {
            dbContext.Database.Migrate();
        }
    }
}
