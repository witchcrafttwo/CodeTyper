namespace CodeTyper.Api.Models;

public sealed record ScoreSubmission(
    string UserId,
    string DisplayName,
    string? TeamId,
    string Scope,
    string Language,
    string Difficulty,
    int CorrectChars,
    double Wpm,
    double Accuracy,
    int MissCount,
    double? Score);
