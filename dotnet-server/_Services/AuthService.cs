using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using TradingScanner._Models.DTOs;
using TradingScanner._Models.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;

namespace TradingScanner._Services;

public class AuthService
{
    private readonly UserManager<User> _userManager;
    private readonly IConfiguration _configuration;

    public AuthService(
        UserManager<User> userManager,
        IConfiguration configuration)
    {
        _userManager = userManager;
        _configuration = configuration;
    }

    public async Task<AuthResponseDto> RegisterAsync(RegisterDto dto)
    {
        var user = new User
        {
            UserName = dto.Email,
            Email = dto.Email,
            FirstName = dto.FirstName,
            LastName = dto.LastName
        };

        var result = await _userManager.CreateAsync(user, dto.Password);

        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(x => x.Description));
            throw new InvalidOperationException(errors);
        }

        return await CreateAuthResponseAsync(user);
    }

    public async Task<AuthResponseDto?> LoginAsync(LoginDto dto)
    {
        var user = await _userManager.FindByEmailAsync(dto.Email);

        if (user is null || !await _userManager.CheckPasswordAsync(user, dto.Password))
        {
            return null;
        }

        return await CreateAuthResponseAsync(user);
    }

    private async Task<AuthResponseDto> CreateAuthResponseAsync(User user)
    {
        var expiresAt = DateTime.UtcNow.AddHours(8);
        var roles = await _userManager.GetRolesAsync(user);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email ?? string.Empty)
        };

        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(
                _configuration["Jwt:Key"]
                ?? throw new InvalidOperationException("Jwt:Key is missing.")));

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: expiresAt,
            signingCredentials: new SigningCredentials(
                key,
                SecurityAlgorithms.HmacSha256));

        return new AuthResponseDto(
            new JwtSecurityTokenHandler().WriteToken(token),
            expiresAt,
            user.Id,
            user.Email ?? string.Empty);
    }
}
