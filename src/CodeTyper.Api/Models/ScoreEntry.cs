namespace CodeTyper.Api.Models;

public sealed record ScoreEntry(
    string ScoreId,
    string UserId,
    string DisplayName,
    string? TeamId,
    string Scope,
    string Language,
    string Difficulty,
    double Wpm,
    double Accuracy,
    double Score,
    DateTimeOffset PlayedAt);
