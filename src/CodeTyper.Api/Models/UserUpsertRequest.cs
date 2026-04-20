namespace CodeTyper.Api.Models;

public sealed record UserUpsertRequest(
    string UserId,
    string Email,
    string DisplayName,
    string? TeamId,
    string? GlobalAlias);
