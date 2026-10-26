using Microsoft.AspNetCore.Identity;

namespace Hangar.Infrastructure.Identity;

public class ApplicationUser : IdentityUser<int>
{
    public required string FullName { get; set; }
}
