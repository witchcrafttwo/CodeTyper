using CodeTyper.Api.Models;

namespace CodeTyper.Api.Data;

public interface IScoreStore
{
    Task SaveScoreAsync(ScoreEntry entry);
    Task<IReadOnlyList<ScoreEntry>> GetRankingAsync(string scope, string language, string difficulty, string? teamId, int top);
}
