using CodeTyper.Api.Models;

namespace CodeTyper.Api.Data;

public interface IUserStore
{
    Task<UserProfile?> GetByIdAsync(string userId);
    Task<UserProfile> UpsertAsync(UserUpsertRequest request);
    Task<UserProfile?> UpdateAliasAsync(string userId, string globalAlias);
}
