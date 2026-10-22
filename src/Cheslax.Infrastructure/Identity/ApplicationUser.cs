using Microsoft.AspNetCore.Identity;

namespace Cheslax.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser
{
    public string DisplayName { get; set; } = string.Empty;
    public string AvatarId { get; set; } = "seal-1";
    public DateTimeOffset CreatedAt { get; set; }
}
