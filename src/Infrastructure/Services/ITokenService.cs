using Infrastructure.Entities;
using System.Security.Claims;

namespace Infrastructure.Services;

public interface ITokenService
{
    string GenerateAccessToken(User user);
    string GenerateRefreshToken();
    ClaimsPrincipal? ValidateToken(string token);
}
