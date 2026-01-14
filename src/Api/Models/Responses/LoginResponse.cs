namespace Api.Models.Responses;

public record LoginResponse
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public required string Email { get; init; }
    public required string AccessToken { get; init; }
    public required string RefreshToken { get; init; }
}
