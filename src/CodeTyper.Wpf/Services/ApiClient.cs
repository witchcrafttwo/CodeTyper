using System.Net.Http;
using System.Net.Http.Json;
using CodeTyper.Wpf.Models;

namespace CodeTyper.Wpf.Services;

public class ApiClient(HttpClient http)
{
    public async Task<List<ModeDefinition>> GetModesAsync() =>
        await http.GetFromJsonAsync<List<ModeDefinition>>("/modes") ?? [];

    public async Task<List<WordEntry>> GetWordsAsync(string language, string difficulty, int count) =>
        await http.GetFromJsonAsync<List<WordEntry>>($"/words?language={language}&difficulty={difficulty}&count={count}") ?? [];

    public async Task<List<WordEntry>> GetAllWordsAsync(string? language, string? difficulty)
    {
        var q = new List<string>();
        if (!string.IsNullOrEmpty(language)) q.Add($"language={language}");
        if (!string.IsNullOrEmpty(difficulty)) q.Add($"difficulty={difficulty}");
        var qs = q.Count > 0 ? "?" + string.Join("&", q) : "";
        return await http.GetFromJsonAsync<List<WordEntry>>($"/admin/words{qs}") ?? [];
    }

    public async Task<ScoreEntry?> SubmitScoreAsync(ScoreSubmission submission)
    {
        var res = await http.PostAsJsonAsync("/scores", submission);
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<ScoreEntry>();
    }

    public async Task<List<ScoreEntry>> GetRankingAsync(string scope, string language, string difficulty, string? teamId, int top)
    {
        var q = $"scope={scope}&language={language}&difficulty={difficulty}&top={top}";
        if (scope == "team" && !string.IsNullOrEmpty(teamId)) q += $"&teamId={teamId}";
        return await http.GetFromJsonAsync<List<ScoreEntry>>($"/rankings?{q}") ?? [];
    }

    public async Task<WordEntry?> AddWordAsync(WordUpsertRequest req)
    {
        var res = await http.PostAsJsonAsync("/admin/words", req);
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<WordEntry>();
    }

    public async Task<WordEntry?> UpdateWordAsync(Guid wordId, WordUpsertRequest req)
    {
        var res = await http.PutAsJsonAsync($"/admin/words/{wordId}", req);
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<WordEntry>();
    }

    public async Task<WordEntry?> DeleteWordAsync(Guid wordId)
    {
        var res = await http.DeleteAsync($"/admin/words/{wordId}");
        res.EnsureSuccessStatusCode();
        return null;
    }

    public async Task UpsertUserAsync(string userId, string displayName, string? teamId)
    {
        var payload = new { userId, email = $"{userId}@codetyper.local", displayName, teamId, globalAlias = (string?)null };
        var res = await http.PostAsJsonAsync("/users/upsert", payload);
        res.EnsureSuccessStatusCode();
    }
}
