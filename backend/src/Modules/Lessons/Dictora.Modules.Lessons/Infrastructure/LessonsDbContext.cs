using Microsoft.EntityFrameworkCore;

namespace Dictora.Modules.Lessons.Infrastructure;

internal sealed class LessonsDbContext(DbContextOptions<LessonsDbContext> options) : DbContext(options)
{
    /// <summary>PostgreSQL schema owned by the Lessons module. Never used by other modules.</summary>
    internal const string Schema = "lessons";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LessonsDbContext).Assembly);
    }
}
