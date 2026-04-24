namespace CodeTyper.Wpf.Models;

public record ModeDefinition(string Language, string Difficulty, string Label);

public record WordEntry(Guid WordId, string Word, string Language, string Difficulty, int Weight, bool Enabled);

public record ScoreEntry(
    string ScoreId, string UserId, string DisplayName, string? TeamId,
    string Scope, string Language, string Difficulty,
    double Wpm, double Accuracy, double Score, DateTimeOffset PlayedAt);

public record ScoreSubmission(
    string UserId, string DisplayName, string? TeamId,
    string Scope, string Language, string Difficulty,
    int CorrectChars, double Wpm, double Accuracy, int MissCount, double? Score);

public record WordUpsertRequest(string Word, string Language, string Difficulty, int Weight, bool Enabled);
