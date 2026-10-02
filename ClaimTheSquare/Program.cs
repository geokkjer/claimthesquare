using ClaimTheSquare.Data;
using ClaimTheSquare.ViewModel;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<TextObjectRepository>();

var app = builder.Build();

// Rekkefølgen er ikke kosmetisk: uten UseDefaultFiles serverer ikke Kestrel
// index.html på "/", og frontenden (som kaller /text-objects med relativ sti)
// ville aldri blitt lastet.
app.UseDefaultFiles();
app.UseStaticFiles();

var repository = app.Services.GetRequiredService<TextObjectRepository>();
var migrateOnStartup = app.Configuration["MIGRATE_ON_STARTUP"] is not "false";

if (migrateOnStartup)
{
    try
    {
        await repository.EnsureSchemaAsync();
    }
    catch (Exception ex)
    {
        // Ikke dø i stillhet: manglende database er den vanligste grunnen til
        // at containeren aldri blir healthy, og loggen er stedet man leser det.
        app.Logger.LogError(ex, "Kunne ikke sikre skjemaet ved oppstart.");
    }
}

// Frontendens kontrakt — se wwwroot/index.html. Endrer du formen her, må du
// endre den der. Det er hele poenget med en kontrakt.
app.MapGet("/text-objects", async (TextObjectRepository repo) =>
    Results.Ok(await repo.GetAllAsync()));

app.MapPost("/text-objects", async (TextObject textObject, TextObjectRepository repo) =>
{
    await repo.SaveAsync(textObject);
    return Results.Ok(true);
});

// ── Identitetskortet ──────────────────────────────────────────────
// APP_VERSION settes av CI til commit-SHA (sha-a1b2c3d). Uten den: "dev".
// Dette er beviset på hvilket bygg som faktisk kjører — samme mønster som
// labApi bruker på /health, og det deploy-jobben leser etter en utrulling.
app.MapGet("/health", (IConfiguration config) => Results.Ok(new
{
    status = "ok",
    version = config["APP_VERSION"] ?? "dev"
}));

app.MapGet("/version.json", (IConfiguration config) => Results.Ok(new
{
    version = config["APP_VERSION"] ?? "dev"
}));

app.Run();
