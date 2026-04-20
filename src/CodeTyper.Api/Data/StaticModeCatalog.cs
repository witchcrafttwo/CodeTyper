using CodeTyper.Api.Models;

namespace CodeTyper.Api.Data;

public sealed class StaticModeCatalog : IModeCatalog
{
    private static readonly IReadOnlyList<ModeDefinition> Modes =
    [
        new("java", "easy", "Java / Easy"),
        new("java", "normal", "Java / Normal"),
        new("java", "hard", "Java / Hard"),
        new("python", "easy", "Python / Easy"),
        new("python", "normal", "Python / Normal"),
        new("python", "hard", "Python / Hard"),
        new("javascript", "easy", "JavaScript / Easy"),
        new("javascript", "normal", "JavaScript / Normal"),
        new("javascript", "hard", "JavaScript / Hard")
    ];

    public IReadOnlyList<ModeDefinition> GetAll() => Modes;
}
