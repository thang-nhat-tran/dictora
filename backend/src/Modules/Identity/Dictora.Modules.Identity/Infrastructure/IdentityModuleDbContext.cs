using Microsoft.EntityFrameworkCore;

namespace Dictora.Modules.Identity.Infrastructure;

// Named IdentityModuleDbContext (not IdentityDbContext) to avoid clashing with
// ASP.NET Core Identity's IdentityDbContext<TUser>, which this class will inherit from in #9.
internal sealed class IdentityModuleDbContext(DbContextOptions<IdentityModuleDbContext> options) : DbContext(options)
{
    /// <summary>PostgreSQL schema owned by the Identity module. Never used by other modules.</summary>
    internal const string Schema = "identity";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityModuleDbContext).Assembly);
    }
}
