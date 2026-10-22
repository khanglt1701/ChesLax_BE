using Cheslax.Application.Abstractions;
using Cheslax.Application.Catalog;
using Cheslax.Application.Common;
using Cheslax.Domain;
using Cheslax.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cheslax.Infrastructure.Catalog;

public sealed class PreferenceService(CheslaxDbContext db, IClock clock) : IPreferenceService
{
    public async Task<UserPreferenceDto> GetAsync(string userId, CancellationToken cancellationToken)
    {
        var preference = await db.UserPreferences.FirstOrDefaultAsync(item => item.UserId == userId, cancellationToken)
            ?? await CreateDefaultAsync(userId, cancellationToken);
        return ToDto(preference);
    }

    public async Task<UserPreferenceDto> UpdateAsync(string userId, UpdatePreferenceRequest request, CancellationToken cancellationToken)
    {
        var preference = await db.UserPreferences.FirstOrDefaultAsync(item => item.UserId == userId, cancellationToken)
            ?? await CreateDefaultAsync(userId, cancellationToken);

        preference.Theme = request.Theme;
        preference.ChessBoardTheme = request.ChessBoardTheme;
        preference.ChessPieceSet = request.ChessPieceSet;
        preference.OAnQuanTheme = request.OAnQuanTheme;
        preference.UpdatedAt = clock.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(preference);
    }

    private async Task<UserPreference> CreateDefaultAsync(string userId, CancellationToken cancellationToken)
    {
        var exists = await db.Users.AnyAsync(user => user.Id == userId, cancellationToken);
        if (!exists)
        {
            throw new CheslaxException("NOT_FOUND", "Không tìm thấy tài khoản.", 404);
        }

        var preference = UserPreference.CreateDefault(userId, clock.UtcNow);
        db.UserPreferences.Add(preference);
        await db.SaveChangesAsync(cancellationToken);
        return preference;
    }

    private static UserPreferenceDto ToDto(UserPreference preference) => new(
        preference.Theme,
        preference.ChessBoardTheme,
        preference.ChessPieceSet,
        preference.OAnQuanTheme,
        preference.UpdatedAt);
}
