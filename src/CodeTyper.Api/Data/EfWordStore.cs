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
            .Select(w => new WordEntry(w.WordId, w.Word, w.Language, w.Difficulty, w.Weight, w.Enabled))
            .ToListAsync();

        if (filtered.Count == 0 || count <= 0) return [];

        var weightedPool = filtered
            .SelectMany(word => Enumerable.Repeat(word, Math.Max(1, word.Weight)))
            .ToArray();

        if (weightedPool.Length == 0) return [];

        var result = new List<WordEntry>(count);
        WordEntry? previous = null;

        for (var i = 0; i < count; i++)
        {
            var candidates = previous is null
                ? weightedPool
                : weightedPool.Where(word => word.WordId != previous.WordId).ToArray();

            if (candidates.Length == 0)
                candidates = weightedPool;

            var next = candidates[Random.Shared.Next(candidates.Length)];
            result.Add(next);
            previous = next;
        }

        return result;
    }

    public async Task<IReadOnlyList<WordEntry>> GetAllWordsAsync(string? language, string? difficulty)
    {
        var query = dbContext.Words.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(language)) query = query.Where(w => w.Language == language);
        if (!string.IsNullOrWhiteSpace(difficulty)) query = query.Where(w => w.Difficulty == difficulty);
        return await query
            .OrderBy(w => w.Language).ThenBy(w => w.Difficulty).ThenBy(w => w.Word)
            .Select(w => new WordEntry(w.WordId, w.Word, w.Language, w.Difficulty, w.Weight, w.Enabled))
            .ToListAsync();
    }

    public async Task<WordEntry?> AddWordAsync(WordUpsertRequest request)
    {
        var entity = new WordEntity
        {
            WordId = Guid.NewGuid(),
            Word = request.Word,
            Language = request.Language,
            Difficulty = request.Difficulty,
            Weight = request.Weight,
            Enabled = request.Enabled
        };
        dbContext.Words.Add(entity);
        await dbContext.SaveChangesAsync();
        return new WordEntry(entity.WordId, entity.Word, entity.Language, entity.Difficulty, entity.Weight, entity.Enabled);
    }

    public async Task<WordEntry?> UpdateWordAsync(Guid wordId, WordUpsertRequest request)
    {
        var entity = await dbContext.Words.FindAsync(wordId);
        if (entity is null) return null;
        entity.Word = request.Word;
        entity.Language = request.Language;
        entity.Difficulty = request.Difficulty;
        entity.Weight = request.Weight;
        entity.Enabled = request.Enabled;
        await dbContext.SaveChangesAsync();
        return new WordEntry(entity.WordId, entity.Word, entity.Language, entity.Difficulty, entity.Weight, entity.Enabled);
    }

    public async Task<bool> DeleteWordAsync(Guid wordId)
    {
        var entity = await dbContext.Words.FindAsync(wordId);
        if (entity is null) return false;
        dbContext.Words.Remove(entity);
        await dbContext.SaveChangesAsync();
        return true;
    }
}
