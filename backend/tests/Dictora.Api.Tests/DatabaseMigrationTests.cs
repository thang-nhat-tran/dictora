using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Dictora.Api.Tests;

/// <summary>
/// Starts the API in Development against a throwaway PostgreSQL (needs Docker) and checks that
/// every module's migrations are applied into the module's own schema (ADR-001).
/// </summary>
public sealed class DatabaseMigrationTests : IAsyncLifetime
{
    private static readonly string[] ModuleSchemas = ["identity", "lessons", "practice"];

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18").Build();

    public Task InitializeAsync() => _postgres.StartAsync();

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    [Fact]
    public async Task Development_startup_migrates_each_module_into_its_own_schema()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
            .UseEnvironment("Development")
            .ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Dictora"] = _postgres.GetConnectionString(),
            })));

        _ = factory.Server; // starts the app, which runs the migrations

        await using var connection = new NpgsqlConnection(_postgres.GetConnectionString());
        await connection.OpenAsync();

        foreach (var schema in ModuleSchemas)
        {
            await using var command = new NpgsqlCommand(
                $"SELECT count(*) FROM \"{schema}\".\"__EFMigrationsHistory\"", connection);
            var applied = (long)(await command.ExecuteScalarAsync())!;

            Assert.True(applied >= 1, $"No migrations applied in schema '{schema}'.");
        }

        await using var publicTables = new NpgsqlCommand(
            "SELECT count(*) FROM information_schema.tables WHERE table_schema = 'public'", connection);
        Assert.Equal(0L, (long)(await publicTables.ExecuteScalarAsync())!);
    }
}
