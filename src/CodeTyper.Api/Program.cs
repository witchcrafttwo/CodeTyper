using System.Text.RegularExpressions;
using CodeTyper.Api.Data;
using CodeTyper.Api.Models;
using CodeTyper.Api.Services;
using Microsoft.EntityFrameworkCore;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

var databaseProvider = builder.Configuration["Database:Provider"] ?? "Sqlite";
if (databaseProvider.Equals("Postgres", StringComparison.OrdinalIgnoreCase))
{
    var conn = ResolvePostgresConnectionString(builder.Configuration);
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
builder.Services.AddScoped<IUserStore, EfUserStore>();

builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();
    await DbSeeder.SeedAsync(db);
}

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapGet("/modes", (IModeCatalog modes) => Results.Ok(modes.GetAll()));

app.MapGet(
    "/words",
    async (string language, string difficulty, int? count, IWordStore words) =>
    {
        var result = await words.GetWordsAsync(language, difficulty, count ?? 20);
        return Results.Ok(result);
    });

app.MapGet(
    "/users/{userId}",
    async (string userId, IUserStore users) =>
    {
        var user = await users.GetByIdAsync(userId);
        return user is null ? Results.NotFound() : Results.Ok(user);
    });

app.MapPost(
    "/users/upsert",
    async (UserUpsertRequest request, IUserStore users) =>
    {
        if (string.IsNullOrWhiteSpace(request.UserId) || string.IsNullOrWhiteSpace(request.DisplayName))
        {
            return Results.BadRequest("userId and displayName are required");
        }

        var result = await users.UpsertAsync(request);
        return Results.Ok(result);
    });

app.MapPatch(
    "/users/{userId}/alias",
    async (string userId, AliasUpdateRequest request, IUserStore users) =>
    {
        if (string.IsNullOrWhiteSpace(request.GlobalAlias) || !Regex.IsMatch(request.GlobalAlias, "^[A-Za-z0-9_]{3,20}$"))
        {
            return Results.BadRequest("globalAlias must match ^[A-Za-z0-9_]{3,20}$");
        }

        var result = await users.UpdateAliasAsync(userId, request.GlobalAlias);
        return result is null ? Results.NotFound() : Results.Ok(result);
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

        if (!submission.Scope.Equals("team", StringComparison.OrdinalIgnoreCase) &&
            !submission.Scope.Equals("global", StringComparison.OrdinalIgnoreCase))
        {
            return Results.BadRequest("scope must be team or global");
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

static string ResolvePostgresConnectionString(IConfiguration config)
{
    var fromConfig = config.GetConnectionString("Postgres");
    if (!string.IsNullOrWhiteSpace(fromConfig))
    {
        return fromConfig;
    }

    var host = config["DB_HOST"];
    var port = config["DB_PORT"] ?? "5432";
    var database = config["DB_NAME"];
    var username = config["DB_USER"];
    var password = config["DB_PASSWORD"];
    var sslMode = config["DB_SSLMODE"] ?? "Require";
    var trustServerCertificate = config.GetValue("DB_TRUST_SERVER_CERT", false);

    if (new[] { host, database, username, password }.Any(string.IsNullOrWhiteSpace))
    {
        throw new InvalidOperationException(
            "PostgreSQL設定が不足しています。ConnectionStrings:Postgres か DB_HOST/DB_NAME/DB_USER/DB_PASSWORD を設定してください。");
    }

    var csb = new NpgsqlConnectionStringBuilder
    {
        Host = host,
        Port = int.Parse(port),
        Database = database,
        Username = username,
        Password = password,
        SslMode = Enum.Parse<SslMode>(sslMode, ignoreCase: true),
        TrustServerCertificate = trustServerCertificate,
        Timeout = 15,
        CommandTimeout = 30,
        Pooling = true,
        MaximumPoolSize = 50
    };

    return csb.ConnectionString;
}

public sealed record AliasUpdateRequest(string GlobalAlias);
