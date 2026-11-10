using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dictora.Shared.Database;

/// <summary>
/// Registers and migrates the per-module DbContexts (see docs/decisions/ADR-001-modular-monolith.md).
/// All modules share one PostgreSQL database; each module gets its own schema and migration history.
/// </summary>
public static class ModuleDatabaseExtensions
{
    /// <summary>Name of the connection string shared by all modules: <c>ConnectionStrings:Dictora</c>.</summary>
    public const string ConnectionStringName = "Dictora";

    /// <summary>
    /// Registers <typeparamref name="TContext"/> for a module: Npgsql, snake_case names,
    /// and the <c>__EFMigrationsHistory</c> table inside the module's own <paramref name="schema"/>.
    /// The context must also call <c>modelBuilder.HasDefaultSchema(schema)</c>.
    /// </summary>
    public static IServiceCollection AddModuleDbContext<TContext>(this IServiceCollection services, string schema)
        where TContext : DbContext
    {
        services.AddDbContext<TContext>((serviceProvider, options) =>
        {
            // Read lazily, so the final configuration (env vars, test overrides) is used.
            var connectionString = serviceProvider.GetRequiredService<IConfiguration>()
                .GetConnectionString(ConnectionStringName);

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    $"Connection string 'ConnectionStrings:{ConnectionStringName}' is not configured. " +
                    $"Set it in appsettings.Development.json or the ConnectionStrings__{ConnectionStringName} environment variable.");
            }

            options
                .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable(HistoryRepository.DefaultTableName, schema))
                .UseSnakeCaseNamingConvention();
        });

        services.AddSingleton(new ModuleDbContextRegistration(typeof(TContext), schema));
        return services;
    }

    /// <summary>Applies pending migrations of every module registered with <see cref="AddModuleDbContext{TContext}"/>.</summary>
    public static async Task MigrateModuleDatabasesAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();

        foreach (var registration in scope.ServiceProvider.GetServices<ModuleDbContextRegistration>())
        {
            var db = (DbContext)scope.ServiceProvider.GetRequiredService(registration.ContextType);
            await db.Database.MigrateAsync(cancellationToken);
        }
    }
}

/// <summary>Marks a DbContext as belonging to a module, so it can be found for migrations.</summary>
public sealed record ModuleDbContextRegistration(Type ContextType, string Schema);
