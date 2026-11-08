using System.Security.Claims;
using MyAfricaMyNarrative.Application;

namespace MyAfricaMyNarrative.Web.Services;

public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public Guid? UserId => Guid.TryParse(accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
