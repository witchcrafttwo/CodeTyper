using CodeTyper.Api.Models;

namespace CodeTyper.Api.Data;

public interface IWordStore
{
    Task<IReadOnlyList<WordEntry>> GetWordsAsync(string language, string difficulty, int count);
}
