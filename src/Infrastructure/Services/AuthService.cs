using Infrastructure.Entities;
using Infrastructure.Repositories;
using Microsoft.AspNetCore.Identity;
using Shared.Abstractions;
using Shared.Primitives;

namespace Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly ITokenService _tokenService;
    private readonly ITimeProvider _timeProvider;
    private readonly IPasswordHasher<User> _passwordHasher;

    public AuthService(
        IUserRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository,
        ITokenService tokenService,
        ITimeProvider timeProvider,
        IPasswordHasher<User> passwordHasher)
    {
        _userRepository = userRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _tokenService = tokenService;
        _timeProvider = timeProvider;
        _passwordHasher = passwordHasher;
    }

    public async Task<Result<(User User, string AccessToken, string RefreshToken)>> LoginAsync(
        string email, string password, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByEmailAsync(email, cancellationToken);
        if (user == null)
            return Result<(User, string, string)>.Failure(Errors.Auth.InvalidCredentials);

        var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (result != PasswordVerificationResult.Success)
            return Result<(User, string, string)>.Failure(Errors.Auth.InvalidCredentials);
        var accessToken = _tokenService.GenerateAccessToken(user);
        var refreshToken = _tokenService.GenerateRefreshToken();

        var refreshTokenEntity = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = refreshToken,
            ExpiresAt = _timeProvider.UtcNow.AddDays(7),
            CreatedAt = _timeProvider.UtcNow
        };

        await _refreshTokenRepository.AddAsync(refreshTokenEntity, cancellationToken);
        await _refreshTokenRepository.SaveChangesAsync(cancellationToken);

        return Result<(User, string, string)>.Success((user, accessToken, refreshToken));
    }

    public async Task<Result<bool>> LogoutAsync(Guid userId, CancellationToken cancellationToken)
    {
        await _refreshTokenRepository.RevokeByUserIdAsync(userId, cancellationToken);
        await _refreshTokenRepository.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }

    public async Task<User?> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await _userRepository.GetByIdAsync(userId, cancellationToken);
    }
}
