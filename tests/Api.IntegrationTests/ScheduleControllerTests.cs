using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Api.Models.Requests;
using Api.Models.Responses;
using FluentAssertions;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Api.IntegrationTests;

public class ScheduleControllerTests : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public ScheduleControllerTests(CustomWebApplicationFactory factory)
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
    public async Task Schedule_HappyPath_CreateUpdateDelete_Assignment()
    {
        // Arrange
        var token = await LoginAsAdminAndGetAccessTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var employeeId = await GetAnyEmployeeIdAsync();
        var date = "2026-02-01";

        // Act 1: upsert workday
        var upsertResponse = await _client.PutAsJsonAsync($"/api/v1/schedule/workdays/{date}", new UpsertWorkDayRequest());

        // Assert 1
        upsertResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var upsertedWorkDay = await upsertResponse.Content.ReadFromJsonAsync<WorkDayResponse>();
        upsertedWorkDay.Should().NotBeNull();
        upsertedWorkDay!.Date.ToString("yyyy-MM-dd").Should().Be(date);

        // Act 2: create assignment
        var createRequest = new CreateWorkDayAssignmentRequest
        {
            EmployeeId = employeeId,
            PortionType = 0,
            Portion = 1.0m,
            Note = "test",
            Worked = null
        };

        var createResponse = await _client.PostAsJsonAsync($"/api/v1/schedule/workdays/{date}/assignments", createRequest);

        // Assert 2
        createResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var created = await createResponse.Content.ReadFromJsonAsync<WorkDayAssignmentResponse>();
        created.Should().NotBeNull();
        created!.Id.Should().NotBeEmpty();
        created.EmployeeId.Should().Be(employeeId);
        created.PortionType.Should().Be(0);
        created.Portion.Should().Be(1.0m);
        created.Note.Should().Be("test");

        // Act 3: get range
        var getResponse = await _client.GetAsync($"/api/v1/schedule/workdays?from={date}&to={date}");

        // Assert 3
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var days = await getResponse.Content.ReadFromJsonAsync<List<WorkDayResponse>>();
        days.Should().NotBeNull();
        var day = days!.Single(d => d.Date.ToString("yyyy-MM-dd") == date);
        day.Assignments.Should().Contain(a => a.Id == created.Id && a.EmployeeId == employeeId);

        // Act 4: patch assignment
        var patchRequest = new UpdateWorkDayAssignmentRequest
        {
            Worked = true,
            Note = "updated"
        };

        var patchResponse = await _client.PatchAsJsonAsync($"/api/v1/schedule/assignments/{created.Id}", patchRequest);

        // Assert 4
        patchResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var patched = await patchResponse.Content.ReadFromJsonAsync<WorkDayAssignmentResponse>();
        patched.Should().NotBeNull();
        patched!.Worked.Should().BeTrue();
        patched.Note.Should().Be("updated");

        // Act 5: delete assignment
        var deleteResponse = await _client.DeleteAsync($"/api/v1/schedule/assignments/{created.Id}");

        // Assert 5
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Act 6: get range again
        var getAfterDeleteResponse = await _client.GetAsync($"/api/v1/schedule/workdays?from={date}&to={date}");

        // Assert 6
        getAfterDeleteResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var daysAfterDelete = await getAfterDeleteResponse.Content.ReadFromJsonAsync<List<WorkDayResponse>>();
        daysAfterDelete.Should().NotBeNull();
        var dayAfterDelete = daysAfterDelete!.Single(d => d.Date.ToString("yyyy-MM-dd") == date);
        dayAfterDelete.Assignments.Should().NotContain(a => a.Id == created.Id);
    }

    [Fact]
    public async Task Schedule_GetWorkDays_WithInvalidFrom_ReturnsBadRequest()
    {
        // Arrange
        var token = await LoginAsAdminAndGetAccessTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync("/api/v1/schedule/workdays?from=bad&to=2026-02-01");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Schedule_CreateAssignment_WithUnknownEmployee_ReturnsNotFound()
    {
        // Arrange
        var token = await LoginAsAdminAndGetAccessTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var date = "2026-02-03";

        var request = new CreateWorkDayAssignmentRequest
        {
            EmployeeId = Guid.NewGuid(),
            PortionType = 0,
            Portion = 1.0m
        };

        // Act
        var response = await _client.PostAsJsonAsync($"/api/v1/schedule/workdays/{date}/assignments", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
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

    private async Task<Guid> GetAnyEmployeeIdAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var employee = await db.Employees
            .AsNoTracking()
            .OrderBy(e => e.LastName)
            .FirstAsync();

        employee.Id.Should().NotBeEmpty();
        return employee.Id;
    }
}
