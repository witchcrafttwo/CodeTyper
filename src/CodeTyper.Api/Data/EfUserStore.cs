using CodeTyper.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace CodeTyper.Api.Data;

public sealed class EfUserStore(AppDbContext dbContext) : IUserStore
{
    public async Task<UserProfile?> GetByIdAsync(string userId)
    {
        var entity = await dbContext.Users.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == userId);
        return entity is null ? null : ToProfile(entity);
    }

    public async Task<UserProfile> UpsertAsync(UserUpsertRequest request)
    {
        var entity = await dbContext.Users.FirstOrDefaultAsync(x => x.UserId == request.UserId);
        if (entity is null)
        {
            entity = new UserEntity
            {
                UserId = request.UserId,
                Email = request.Email,
                DisplayName = request.DisplayName,
                TeamId = request.TeamId,
                GlobalAlias = request.GlobalAlias,
                CreatedAt = DateTimeOffset.UtcNow
            };
            dbContext.Users.Add(entity);
        }
        else
        {
            entity.Email = request.Email;
            entity.DisplayName = request.DisplayName;
            entity.TeamId = request.TeamId;
            entity.GlobalAlias = request.GlobalAlias;
        }

        await dbContext.SaveChangesAsync();
        return ToProfile(entity);
    }

    public async Task<UserProfile?> UpdateAliasAsync(string userId, string globalAlias)
    {
        var entity = await dbContext.Users.FirstOrDefaultAsync(x => x.UserId == userId);
        if (entity is null)
        {
            return null;
        }

        entity.GlobalAlias = globalAlias;
        await dbContext.SaveChangesAsync();
        return ToProfile(entity);
    }

    private static UserProfile ToProfile(UserEntity entity)
    {
        return new UserProfile(entity.UserId, entity.Email, entity.DisplayName, entity.TeamId, entity.GlobalAlias, entity.CreatedAt);
    }
}
