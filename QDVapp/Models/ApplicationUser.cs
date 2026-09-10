using Microsoft.AspNetCore.Identity;

namespace QDVapp.Models;

public class ApplicationUser : IdentityUser
{
    // Theme: true = bright (1), false = dark (0), null = not set (default to light)
    public bool? Theme { get; set; }
}
