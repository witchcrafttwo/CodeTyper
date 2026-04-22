namespace CodeTyper.Api.Models;

public sealed record WordEntry(Guid WordId, string Word, string Language, string Difficulty, int Weight, bool Enabled);
