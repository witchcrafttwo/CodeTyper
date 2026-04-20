using CodeTyper.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace CodeTyper.Api.Data;

public sealed class EfWordStore(AppDbContext dbContext) : IWordStore
{
    public async Task<IReadOnlyList<WordEntry>> GetWordsAsync(string language, string difficulty, int count)
    {
        var filtered = await dbContext.Words
            .AsNoTracking()
            .Where(w => w.Enabled)
            .Where(w => w.Language == language && w.Difficulty == difficulty)
            .Select(w => new WordEntry(w.Word, w.Language, w.Difficulty, w.Weight))
            .ToListAsync();

        if (filtered.Count == 0)
        {
            return [];
        }

        return filtered
            .SelectMany(x => Enumerable.Repeat(x, Math.Max(1, x.Weight)))
            .OrderBy(_ => Random.Shared.Next())
            .Take(count)
            .ToList();
    }
}
