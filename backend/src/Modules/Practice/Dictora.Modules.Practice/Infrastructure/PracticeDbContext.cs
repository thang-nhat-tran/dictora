using Microsoft.EntityFrameworkCore;

namespace Dictora.Modules.Practice.Infrastructure;

internal sealed class PracticeDbContext(DbContextOptions<PracticeDbContext> options) : DbContext(options)
{
    /// <summary>PostgreSQL schema owned by the Practice module. Never used by other modules.</summary>
    internal const string Schema = "practice";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PracticeDbContext).Assembly);
    }
}
