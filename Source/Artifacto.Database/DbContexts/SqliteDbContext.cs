using Microsoft.EntityFrameworkCore;

namespace Artifacto.Database.DbContexts;

/// <summary>
/// SQLite-specific implementation of the <see cref="ArtifactoDbContext"/>.
/// Configures the database context to use SQLite as the data provider.
/// </summary>
public class SqliteDbContext : ArtifactoDbContext
{
    private const string SqliteConnectionString = "Data Source=/data/artifacto.db;";
    
    /// <summary>
    /// Configures the database context to use SQLite.
    /// </summary>
    /// <param name="optionsBuilder">The options builder used to configure the context.</param>
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseSqlite(SqliteConnectionString,
                o => o.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery));
        }
    }
}
