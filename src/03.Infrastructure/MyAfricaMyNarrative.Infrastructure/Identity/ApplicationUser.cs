using Microsoft.AspNetCore.Identity;

namespace MyAfricaMyNarrative.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class ApplicationRole : IdentityRole<Guid> { }
