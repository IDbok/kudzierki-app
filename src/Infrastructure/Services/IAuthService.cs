using Infrastructure.Entities;
using Shared.Primitives;

namespace Infrastructure.Services;

public interface IAuthService
{
    Task<Result<(User User, string AccessToken, string RefreshToken)>> LoginAsync(
        string email, string password, CancellationToken cancellationToken = default);
    Task<Result<bool>> LogoutAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<User?> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken = default);
}
