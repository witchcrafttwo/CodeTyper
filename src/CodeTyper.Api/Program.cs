using System.Security.Claims;
using System.Text.RegularExpressions;
using CodeTyper.Api.Data;
using CodeTyper.Api.Models;
using CodeTyper.Api.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);
var adminPassword = builder.Configuration["Admin:Password"] ?? "5432";

// ── Database ──────────────────────────────────────────────────────────────────
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

// ── Authentication (Cookie only — Google OAuth は後で追加) ────────────────────
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "codetyper_auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.Events.OnRedirectToLogin = ctx =>
        {
            ctx.Response.StatusCode = 401;
            return Task.CompletedTask;
        };
    });

builder.Services.AddAuthorization();

// ── App Services ──────────────────────────────────────────────────────────────
builder.Services.AddSingleton<IModeCatalog, StaticModeCatalog>();
builder.Services.AddScoped<IWordStore, EfWordStore>();
builder.Services.AddScoped<IScoreStore, EfScoreStore>();
builder.Services.AddScoped<IUserStore, EfUserStore>();
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

// ── DB Init ───────────────────────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();
}

app.UseAuthentication();
app.UseAuthorization();

// ── Auth Endpoints ────────────────────────────────────────────────────────────
app.MapGet("/auth/login", () => Results.Redirect("/"));  // TODO: Google OAuth

app.MapGet("/auth/logout", async (HttpContext ctx) =>
{
    await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/");
});

app.MapGet("/auth/me", (HttpContext ctx) =>
{
    if (ctx.User.Identity?.IsAuthenticated != true)
        return Results.Ok(new { authenticated = false });

    var userId = $"google:{ctx.User.FindFirstValue(ClaimTypes.NameIdentifier)}";
    var email = ctx.User.FindFirstValue(ClaimTypes.Email);
    var name = ctx.User.FindFirstValue(ClaimTypes.Name);
    var picture = ctx.User.FindFirstValue("urn:google:picture") ?? "";

    return Results.Ok(new { authenticated = true, userId, email, name, picture });
});

// ── General Endpoints ─────────────────────────────────────────────────────────
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapGet("/modes", (IModeCatalog modes) => Results.Ok(modes.GetAll()));

app.MapGet("/words", async (string language, string difficulty, int? count, IWordStore words) =>
{
    var result = await words.GetWordsAsync(language, difficulty, count ?? 20);
    return Results.Ok(result);
});

// ── Word Management (Admin) ───────────────────────────────────────────────────
app.MapGet("/admin/words", async (HttpRequest request, string? language, string? difficulty, IWordStore words) =>
{
    if (!HasValidAdminPassword(request, adminPassword))
        return Results.Unauthorized();

    var result = await words.GetAllWordsAsync(language, difficulty);
    return Results.Ok(result);
});

app.MapPost("/admin/words", async (HttpRequest httpRequest, WordUpsertRequest request, IWordStore words) =>
{
    if (!HasValidAdminPassword(httpRequest, adminPassword))
        return Results.Unauthorized();

    if (string.IsNullOrWhiteSpace(request.Word) || string.IsNullOrWhiteSpace(request.Language) || string.IsNullOrWhiteSpace(request.Difficulty))
        return Results.BadRequest("word, language, difficulty are required");
    var result = await words.AddWordAsync(request);
    return Results.Ok(result);
});

app.MapPut("/admin/words/{wordId:guid}", async (HttpRequest httpRequest, Guid wordId, WordUpsertRequest request, IWordStore words) =>
{
    if (!HasValidAdminPassword(httpRequest, adminPassword))
        return Results.Unauthorized();

    var result = await words.UpdateWordAsync(wordId, request);
    return result is null ? Results.NotFound() : Results.Ok(result);
});

app.MapDelete("/admin/words/{wordId:guid}", async (HttpRequest request, Guid wordId, IWordStore words) =>
{
    if (!HasValidAdminPassword(request, adminPassword))
        return Results.Unauthorized();

    var deleted = await words.DeleteWordAsync(wordId);
    return deleted ? Results.NoContent() : Results.NotFound();
});

// ── User Endpoints ────────────────────────────────────────────────────────────
app.MapGet("/users/{userId}", async (string userId, IUserStore users) =>
{
    var user = await users.GetByIdAsync(userId);
    return user is null ? Results.NotFound() : Results.Ok(user);
});

app.MapPost("/users/upsert", async (UserUpsertRequest request, IUserStore users) =>
{
    if (string.IsNullOrWhiteSpace(request.UserId) || string.IsNullOrWhiteSpace(request.DisplayName))
        return Results.BadRequest("userId and displayName are required");
    var result = await users.UpsertAsync(request);
    return Results.Ok(result);
});

app.MapPatch("/users/{userId}/alias", async (string userId, AliasUpdateRequest request, IUserStore users) =>
{
    if (string.IsNullOrWhiteSpace(request.GlobalAlias) || !Regex.IsMatch(request.GlobalAlias, "^[A-Za-z0-9_]{3,20}$"))
        return Results.BadRequest("globalAlias must match ^[A-Za-z0-9_]{3,20}$");
    var result = await users.UpdateAliasAsync(userId, request.GlobalAlias);
    return result is null ? Results.NotFound() : Results.Ok(result);
});

// ── Score Endpoints ───────────────────────────────────────────────────────────
app.MapPost("/scores", async (ScoreSubmission submission, IScoreStore scoreStore) =>
{
    if (string.IsNullOrWhiteSpace(submission.UserId))
        return Results.BadRequest("userId is required");
    if (string.IsNullOrWhiteSpace(submission.Language) || string.IsNullOrWhiteSpace(submission.Difficulty))
        return Results.BadRequest("language and difficulty are required");
    if (!submission.Scope.Equals("team", StringComparison.OrdinalIgnoreCase) &&
        !submission.Scope.Equals("global", StringComparison.OrdinalIgnoreCase))
        return Results.BadRequest("scope must be team or global");

    var calculated = submission.Score ?? ScoreCalculator.Calculate(
        submission.CorrectChars, submission.Wpm, submission.Accuracy, submission.MissCount);

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

    var saved = await scoreStore.SaveScoreAsync(entry);
    return Results.Ok(saved);
});

app.MapGet("/rankings", async (string scope, string language, string difficulty, string? teamId, int? top, IScoreStore scoreStore) =>
{
    var rows = await scoreStore.GetRankingAsync(scope, language, difficulty, teamId, top ?? 20);
    return Results.Ok(rows);
});

app.Run();

// ── Helpers ───────────────────────────────────────────────────────────────────
static string ResolvePostgresConnectionString(IConfiguration config)
{
    var fromConfig = config.GetConnectionString("Postgres");
    if (!string.IsNullOrWhiteSpace(fromConfig)) return fromConfig;

    var host = config["DB_HOST"];
    var port = config["DB_PORT"] ?? "5432";
    var database = config["DB_NAME"];
    var username = config["DB_USER"];
    var password = config["DB_PASSWORD"];
    var sslMode = config["DB_SSLMODE"] ?? "Require";
    var trustServerCertificate = config.GetValue("DB_TRUST_SERVER_CERT", false);

    if (new[] { host, database, username, password }.Any(string.IsNullOrWhiteSpace))
        throw new InvalidOperationException("PostgreSQL設定が不足しています。");

    return new NpgsqlConnectionStringBuilder
    {
        Host = host, Port = int.Parse(port!), Database = database,
        Username = username, Password = password,
        SslMode = Enum.Parse<SslMode>(sslMode, ignoreCase: true),
        Timeout = 15, CommandTimeout = 30, Pooling = true, MaxPoolSize = 50
    }.ConnectionString;
}

static bool HasValidAdminPassword(HttpRequest request, string expectedPassword)
{
    return request.Headers.TryGetValue("X-Admin-Password", out var provided) &&
           provided.Any(value => value == expectedPassword);
}

public sealed record AliasUpdateRequest(string GlobalAlias);
