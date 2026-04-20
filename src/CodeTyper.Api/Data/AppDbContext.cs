using CodeTyper.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace CodeTyper.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<UserEntity> Users => Set<UserEntity>();
    public DbSet<WordEntity> Words => Set<WordEntity>();
    public DbSet<ScoreEntity> Scores => Set<ScoreEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserEntity>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(x => x.UserId);
            entity.Property(x => x.UserId).HasColumnName("user_id").HasMaxLength(128);
            entity.Property(x => x.Email).HasColumnName("email").HasMaxLength(256);
            entity.Property(x => x.DisplayName).HasColumnName("display_name").HasMaxLength(64);
            entity.Property(x => x.TeamId).HasColumnName("team_id").HasMaxLength(64);
            entity.Property(x => x.GlobalAlias).HasColumnName("global_alias").HasMaxLength(64);
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
        });

        modelBuilder.Entity<WordEntity>(entity =>
        {
            entity.ToTable("words");
            entity.HasKey(x => x.WordId);
            entity.Property(x => x.WordId).HasColumnName("word_id");
            entity.Property(x => x.Word).HasColumnName("word").HasMaxLength(128);
            entity.Property(x => x.Language).HasColumnName("language").HasMaxLength(32);
            entity.Property(x => x.Difficulty).HasColumnName("difficulty").HasMaxLength(16);
            entity.Property(x => x.Weight).HasColumnName("weight");
            entity.Property(x => x.Enabled).HasColumnName("enabled");
            entity.HasIndex(x => new { x.Language, x.Difficulty });
        });

        modelBuilder.Entity<ScoreEntity>(entity =>
        {
            entity.ToTable("scores");
            entity.HasKey(x => x.ScoreId);
            entity.Property(x => x.ScoreId).HasColumnName("score_id").HasMaxLength(64);
            entity.Property(x => x.UserId).HasColumnName("user_id").HasMaxLength(128);
            entity.Property(x => x.DisplayName).HasColumnName("display_name").HasMaxLength(64);
            entity.Property(x => x.TeamId).HasColumnName("team_id").HasMaxLength(64);
            entity.Property(x => x.Scope).HasColumnName("scope").HasMaxLength(16);
            entity.Property(x => x.Language).HasColumnName("language").HasMaxLength(32);
            entity.Property(x => x.Difficulty).HasColumnName("difficulty").HasMaxLength(16);
            entity.Property(x => x.Wpm).HasColumnName("wpm").HasPrecision(8, 2);
            entity.Property(x => x.Accuracy).HasColumnName("accuracy").HasPrecision(5, 2);
            entity.Property(x => x.Score).HasColumnName("score").HasPrecision(10, 2);
            entity.Property(x => x.PlayedAt).HasColumnName("played_at");

            entity.HasIndex(x => new { x.Scope, x.Language, x.Difficulty, x.TeamId, x.Score });
            entity.HasOne<UserEntity>()
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}

public sealed class UserEntity
{
    public string UserId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? TeamId { get; set; }
    public string? GlobalAlias { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class WordEntity
{
    public Guid WordId { get; set; }
    public string Word { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public string Difficulty { get; set; } = string.Empty;
    public int Weight { get; set; } = 1;
    public bool Enabled { get; set; } = true;
}

public sealed class ScoreEntity
{
    public string ScoreId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? TeamId { get; set; }
    public string Scope { get; set; } = "global";
    public string Language { get; set; } = string.Empty;
    public string Difficulty { get; set; } = string.Empty;
    public double Wpm { get; set; }
    public double Accuracy { get; set; }
    public double Score { get; set; }
    public DateTimeOffset PlayedAt { get; set; }

    public ScoreEntry ToModel() => new(
        ScoreId,
        UserId,
        DisplayName,
        TeamId,
        Scope,
        Language,
        Difficulty,
        Wpm,
        Accuracy,
        Score,
        PlayedAt);
}
