using CodeTyper.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace CodeTyper.Api.Data;

public sealed class EfScoreStore(AppDbContext dbContext) : IScoreStore
{
    public async Task<ScoreEntry> SaveScoreAsync(ScoreEntry entry)
    {
        var existing = await dbContext.Scores
            .FirstOrDefaultAsync(x =>
                x.UserId == entry.UserId &&
                x.Scope == entry.Scope &&
                x.Language == entry.Language &&
                x.Difficulty == entry.Difficulty &&
                x.TeamId == entry.TeamId);

        if (existing is null)
        {
            dbContext.Scores.Add(new ScoreEntity
            {
                ScoreId = entry.ScoreId,
                UserId = entry.UserId,
                DisplayName = entry.DisplayName,
                TeamId = entry.TeamId,
                Scope = entry.Scope,
                Language = entry.Language,
                Difficulty = entry.Difficulty,
                Wpm = entry.Wpm,
                Accuracy = entry.Accuracy,
                Score = entry.Score,
                PlayedAt = entry.PlayedAt
            });

            await dbContext.SaveChangesAsync();
            return entry;
        }

        var shouldUpdate =
            entry.Score > existing.Score ||
            (entry.Score == existing.Score && entry.Wpm > existing.Wpm) ||
            (entry.Score == existing.Score && entry.Wpm == existing.Wpm && entry.Accuracy > existing.Accuracy);

        if (shouldUpdate)
        {
            existing.DisplayName = entry.DisplayName;
            existing.TeamId = entry.TeamId;
            existing.Wpm = entry.Wpm;
            existing.Accuracy = entry.Accuracy;
            existing.Score = entry.Score;
            existing.PlayedAt = entry.PlayedAt;

            await dbContext.SaveChangesAsync();
            return existing.ToModel();
        }

        return existing.ToModel();
    }

    public async Task<IReadOnlyList<ScoreEntry>> GetRankingAsync(string scope, string language, string difficulty, string? teamId, int top)
    {
        var query = dbContext.Scores
            .AsNoTracking()
            .Where(x => x.Scope == scope)
            .Where(x => x.Language == language)
            .Where(x => x.Difficulty == difficulty);

        if (scope.Equals("team", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(x => x.TeamId == teamId);
        }

        var rows = await query
            .Select(x => x.ToModel())
            .ToListAsync();

        return rows
            .GroupBy(x => x.UserId)
            .Select(group => group
                .OrderByDescending(x => x.Score)
                .ThenByDescending(x => x.Wpm)
                .ThenByDescending(x => x.Accuracy)
                .ThenByDescending(x => x.PlayedAt)
                .First())
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Wpm)
            .ThenByDescending(x => x.Accuracy)
            .ThenByDescending(x => x.PlayedAt)
            .Take(top)
            .ToList();
    }
}
