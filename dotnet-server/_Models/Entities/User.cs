using Microsoft.AspNetCore.Identity;

namespace TradingScanner._Models.Entities;

public class User : IdentityUser<int>
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
}
