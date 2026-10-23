using Microsoft.AspNetCore.Identity;

namespace Cheslax.Infrastructure.Identity;

public sealed class User : IdentityUser
{
    public string DisplayName { get; set; } = string.Empty;
    public string AvatarId { get; set; } = "seal-1";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LastSeenAt { get; set; }
    public bool IsGuest { get; set; }
    public bool IsBot { get; set; }
}
