using CodeTyper.Api.Models;

namespace CodeTyper.Api.Data;

public interface IWordStore
{
    Task<IReadOnlyList<WordEntry>> GetWordsAsync(string language, string difficulty, int count);
    Task<IReadOnlyList<WordEntry>> GetAllWordsAsync(string? language, string? difficulty);
    Task<WordEntry?> AddWordAsync(WordUpsertRequest request);
    Task<WordEntry?> UpdateWordAsync(Guid wordId, WordUpsertRequest request);
    Task<bool> DeleteWordAsync(Guid wordId);
}
