using System.Net;
using System.Net.Http.Json;
using FluentAssertions;

namespace Api.IntegrationTests;

public class HealthControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public HealthControllerTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Health_ReturnsHealthyStatus()
    {
        // Act
        var response = await _client.GetAsync("/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var healthResponse = await response.Content.ReadFromJsonAsync<HealthResponse>();
        healthResponse.Should().NotBeNull();
        healthResponse!.Status.Should().Be("healthy");
        healthResponse.Database.Should().Be("connected");
    }

    [Fact]
    public async Task Health_ReturnsCorrelationId()
    {
        // Arrange
        var correlationId = Guid.NewGuid().ToString();
        _client.DefaultRequestHeaders.Add("X-Correlation-ID", correlationId);

        // Act
        var response = await _client.GetAsync("/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Should().ContainKey("X-Correlation-ID");
        response.Headers.GetValues("X-Correlation-ID").First().Should().Be(correlationId);
    }

    [Fact]
    public async Task Health_GeneratesCorrelationIdWhenNotProvided()
    {
        // Act
        var response = await _client.GetAsync("/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Should().ContainKey("X-Correlation-ID");
        var correlationId = response.Headers.GetValues("X-Correlation-ID").First();
        correlationId.Should().NotBeNullOrEmpty();
        Guid.TryParse(correlationId, out _).Should().BeTrue();
    }

    private record HealthResponse(string Status, string Database);
}
