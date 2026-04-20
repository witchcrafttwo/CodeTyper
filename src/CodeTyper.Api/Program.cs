using CodeTyper.Api.Data;
using CodeTyper.Api.Models;
using CodeTyper.Api.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var databaseProvider = builder.Configuration["Database:Provider"] ?? "Sqlite";
if (databaseProvider.Equals("Postgres", StringComparison.OrdinalIgnoreCase))
{
    var conn = builder.Configuration.GetConnectionString("Postgres")
               ?? throw new InvalidOperationException("ConnectionStrings:Postgres is required.");
    builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(conn));
}
else
{
    var conn = builder.Configuration.GetConnectionString("Sqlite") ?? "Data Source=codetyper.db";
    builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite(conn));
}

builder.Services.AddSingleton<IModeCatalog, StaticModeCatalog>();
builder.Services.AddScoped<IWordStore, EfWordStore>();
builder.Services.AddScoped<IScoreStore, EfScoreStore>();

builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();
    await DbSeeder.SeedAsync(db);
}

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapGet("/modes", (IModeCatalog modes) => Results.Ok(modes.GetAll()));

app.MapGet(
    "/words",
    async (string language, string difficulty, int? count, IWordStore words) =>
    {
        var result = await words.GetWordsAsync(language, difficulty, count ?? 20);
        return Results.Ok(result);
    });

app.MapPost(
    "/scores",
    async (ScoreSubmission submission, IScoreStore scoreStore) =>
    {
        if (string.IsNullOrWhiteSpace(submission.UserId))
        {
            return Results.BadRequest("userId is required");
        }

        if (string.IsNullOrWhiteSpace(submission.Language) || string.IsNullOrWhiteSpace(submission.Difficulty))
        {
            return Results.BadRequest("language and difficulty are required");
        }

        var calculated = ScoreCalculator.Calculate(
            submission.CorrectChars,
            submission.Wpm,
            submission.Accuracy,
            submission.MissCount);

        var entry = new ScoreEntry(
            Guid.NewGuid().ToString("N"),
            submission.UserId,
            submission.DisplayName,
            submission.TeamId,
            submission.Scope,
            submission.Language,
            submission.Difficulty,
            submission.Wpm,
            submission.Accuracy,
            calculated,
            DateTimeOffset.UtcNow);

        await scoreStore.SaveScoreAsync(entry);
        return Results.Ok(entry);
    });

app.MapGet(
    "/rankings",
    async (string scope, string language, string difficulty, string? teamId, int? top, IScoreStore scoreStore) =>
    {
        var rows = await scoreStore.GetRankingAsync(scope, language, difficulty, teamId, top ?? 20);
        return Results.Ok(rows);
    });

app.Run();
