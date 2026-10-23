namespace Cheslax.Application.Auth;

public interface IAuthService
{
    Task<UserDto> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken);
    Task<UserDto> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
    Task LogoutAsync(CancellationToken cancellationToken);
    Task<ProfileDto> GetCurrentAsync(string userId, CancellationToken cancellationToken);
    Task<ProfileDto> UpdateCurrentAsync(string userId, UpdateProfileRequest request, CancellationToken cancellationToken);
    Task<ProfileDto?> GetPublicAsync(string username, CancellationToken cancellationToken);
    Task<UserDto> EnsureGuestAsync(GuestRequest request, string? currentUserId, CancellationToken cancellationToken);
}
