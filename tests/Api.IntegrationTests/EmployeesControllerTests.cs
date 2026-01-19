using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Api.Models.Requests;
using Api.Models.Responses;
using FluentAssertions;

namespace Api.IntegrationTests;

public class EmployeesControllerTests : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public EmployeesControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await _factory.ResetDatabaseAsync();
    }

    [Fact]
    public async Task Employees_GetAll_WithoutFilters_ReturnsOk()
    {
        // Arrange
        var token = await LoginAsAdminAndGetAccessTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync("/api/v1/employees");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var employees = await response.Content.ReadFromJsonAsync<List<EmployeeResponse>>();
        employees.Should().NotBeNull();
        employees!.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Employees_GetAll_WithPositionFilter_ReturnsOnlyThatPosition()
    {
        // Arrange
        var token = await LoginAsAdminAndGetAccessTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync("/api/v1/employees?position=Cleaner");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var employees = await response.Content.ReadFromJsonAsync<List<EmployeeResponse>>();
        employees.Should().NotBeNull();
        employees!.Should().NotBeEmpty();
        employees.Should().OnlyContain(e => e.Position == 3);
    }

    [Fact]
    public async Task Employees_GetAll_WithIsActiveTrue_ReturnsOnlyLinkedToUser()
    {
        // Arrange
        var token = await LoginAsAdminAndGetAccessTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync("/api/v1/employees?isActive=true");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var employees = await response.Content.ReadFromJsonAsync<List<EmployeeResponse>>();
        employees.Should().NotBeNull();
        employees!.Should().NotBeEmpty();
        employees.Should().HaveCount(1);
    }

    [Fact]
    public async Task Employees_GetAll_WithIsActiveFalse_ReturnsOnlyNotLinkedToUser()
    {
        // Arrange
        var token = await LoginAsAdminAndGetAccessTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync("/api/v1/employees?isActive=false");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var employees = await response.Content.ReadFromJsonAsync<List<EmployeeResponse>>();
        employees.Should().NotBeNull();
        employees!.Should().NotBeEmpty();
        employees.Should().HaveCount(2);
    }

    [Fact]
    public async Task Employees_GetAll_WithCombinedFilters_Works()
    {
        // Arrange
        var token = await LoginAsAdminAndGetAccessTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync("/api/v1/employees?isActive=false&position=Administrator");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var employees = await response.Content.ReadFromJsonAsync<List<EmployeeResponse>>();
        employees.Should().NotBeNull();
        employees!.Should().HaveCount(1);
        employees.Single().Position.Should().Be(1);
    }

    private async Task<string> LoginAsAdminAndGetAccessTokenAsync()
    {
        var loginRequest = new LoginRequest { Email = "admin@local", Password = "Admin123!" };
        var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login", loginRequest);
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var tokens = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        tokens.Should().NotBeNull();
        tokens!.AccessToken.Should().NotBeNullOrEmpty();

        return tokens.AccessToken;
    }
}
