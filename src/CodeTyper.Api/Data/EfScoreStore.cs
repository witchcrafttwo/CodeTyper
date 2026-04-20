using CodeTyper.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace CodeTyper.Api.Data;

public sealed class EfScoreStore(AppDbContext dbContext) : IScoreStore
{
    public async Task SaveScoreAsync(ScoreEntry entry)
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

        return await query
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.PlayedAt)
            .Take(top)
            .Select(x => x.ToModel())
            .ToListAsync();
    }
}
