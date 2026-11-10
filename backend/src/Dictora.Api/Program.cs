using Dictora.Modules.Identity;
using Dictora.Modules.Lessons;
using Dictora.Modules.Practice;
using Dictora.Shared.Database;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

// Modules. The host only wires them up; it holds no business logic.
builder.Services
    .AddIdentityModule(builder.Configuration)
    .AddLessonsModule(builder.Configuration)
    .AddPracticeModule(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    // Local dev convenience: apply every module's pending migrations on startup.
    // Staging/production run migrations as a deploy step instead (#7).
    await app.Services.MigrateModuleDatabasesAsync();
}

app.MapHealthChecks("/health");

app.MapIdentityEndpoints();
app.MapLessonsEndpoints();
app.MapPracticeEndpoints();

await app.RunAsync();
