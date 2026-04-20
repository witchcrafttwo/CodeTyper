namespace CodeTyper.Api.Models;

public sealed record UserProfile(
    string UserId,
    string Email,
    string DisplayName,
    string? TeamId,
    string? GlobalAlias,
    DateTimeOffset CreatedAt);
