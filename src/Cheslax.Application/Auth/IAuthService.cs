namespace Cheslax.Application.Auth;

public interface IAuthService
{
    Task<UserDto> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken);
    Task<UserDto> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
    Task LogoutAsync(CancellationToken cancellationToken);
    Task<UserDto> GetCurrentAsync(string userId, CancellationToken cancellationToken);
    Task<UserDto> UpdateCurrentAsync(string userId, UpdateProfileRequest request, CancellationToken cancellationToken);
    Task<PublicProfileDto?> GetPublicAsync(string username, CancellationToken cancellationToken);
}
