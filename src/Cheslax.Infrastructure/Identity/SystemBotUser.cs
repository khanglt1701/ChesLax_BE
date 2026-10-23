using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Cheslax.Infrastructure.Identity;

public static class SystemBotUser
{
    public const string UserId = "cheslax-ai-opponent";
    public const string UserName = "cheslax_ai";
    public const string Email = "ai@cheslax.local";
    public const string DisplayName = "Máy Cheslax";

    public static bool IsBotId(string? userId) =>
        string.Equals(userId, UserId, StringComparison.Ordinal);

    public static async Task<User> EnsureAsync(UserManager<User> users, CancellationToken cancellationToken)
    {
        var existing = await users.Users.FirstOrDefaultAsync(user => user.Id == UserId, cancellationToken);
        if (existing is not null)
        {
            if (!existing.IsBot)
            {
                existing.IsBot = true;
                existing.IsGuest = true;
                await users.UpdateAsync(existing);
            }

            return existing;
        }

        var created = new User
        {
            Id = UserId,
            UserName = UserName,
            Email = Email,
            DisplayName = DisplayName,
            AvatarId = "seal-1",
            CreatedAt = DateTimeOffset.UtcNow,
            EmailConfirmed = true,
            IsGuest = true,
            IsBot = true,
        };
        var result = await users.CreateAsync(created);
        if (!result.Succeeded)
        {
            existing = await users.Users.FirstOrDefaultAsync(
                user => user.Id == UserId || user.NormalizedUserName == UserName.ToUpperInvariant(),
                cancellationToken);
            if (existing is not null)
            {
                return existing;
            }

            throw new InvalidOperationException("Không tạo được tài khoản máy Cheslax: " + string.Join("; ", result.Errors.Select(error => error.Description)));
        }

        return created;
    }
}
