namespace CodeTyper.Api.Models;

public sealed record WordUpsertRequest(
    string Word,
    string Language,
    string Difficulty,
    int Weight,
    bool Enabled);
