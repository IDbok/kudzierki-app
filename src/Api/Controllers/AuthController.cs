using Api.Models.Requests;
using Api.Models.Responses;
using Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthService authService, ILogger<AuthController> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        _logger.LogInformation("Login attempt for email: {Email}", request.Email);

        var result = await _authService.LoginAsync(request.Email, request.Password, cancellationToken);

        if (result.IsFailure)
        {
            _logger.LogWarning("Login failed for email: {Email}. Error: {Error}",
                request.Email, result.Error!.Code);
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Authentication failed",
                Detail = result.Error.Message
            });
        }

        var response = new LoginResponse
        {
            Id = result.Value.User.Id.ToString(),
            Email = result.Value.User.Email,
            AccessToken = result.Value.AccessToken,
            RefreshToken = result.Value.RefreshToken,
            DisplayName = "todo" // TODO: Add display name to User entity
        };

        _logger.LogInformation("Login successful for email: {Email}", request.Email);
        return Ok(response);
    }

    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        _logger.LogInformation("Logout initiated for user: {UserId}", userId);

        await _authService.LogoutAsync(userId, cancellationToken);

        _logger.LogInformation("Logout successful for user: {UserId}", userId);
        return Ok(new { message = "Logged out successfully" });
    }

    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetCurrentUser(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var user = await _authService.GetCurrentUserAsync(userId, cancellationToken);

        if (user == null)
            return Unauthorized();

        return Ok(new UserResponse(user.Id, user.Email, user.Role.ToString()));
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.Parse(userIdClaim!);
    }
}
