using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Api.Models.Requests;
using Api.Models.Responses;
using FluentAssertions;
using Infrastructure.Integrations.Altegio;
using Infrastructure.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Api.IntegrationTests;

public class AltegioControllerTests : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory;
    private HttpClient _client = null!;

    public AltegioControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        _client = CreateClientWithMockedAltegio();
        var token = await LoginAsAdminAndGetAccessTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    public async Task DisposeAsync()
    {
        await _factory.ResetDatabaseAsync();
    }

    [Fact]
    public async Task Altegio_GetEmployeeSchedule_ReturnsNormalizedPayload()
    {
        var response = await _client.GetAsync("/api/v1/altegio/employees/2094973/schedule?date=2026-02-12");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<AltegioEmployeeScheduleResponse>();

        payload.Should().NotBeNull();
        payload!.EmployeeId.Should().Be(2094973);
        payload.Date.Should().Be(new DateOnly(2026, 2, 12));
        payload.Records.Should().HaveCount(1);
        payload.Records[0].ClientName.Should().Be("Anna Smith");
        payload.Records[0].TotalCost.Should().Be(100m);
        payload.Records[0].TotalCostToPay.Should().Be(90m);
    }

    [Fact]
    public async Task Altegio_GetEmployeeSalary_ReturnsAggregatedTotal()
    {
        var response = await _client.GetAsync("/api/v1/altegio/employees/2094973/salary?from=2026-02-10&to=2026-02-11");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<AltegioSalaryResponse>();

        payload.Should().NotBeNull();
        payload!.EmployeeId.Should().Be(2094973);
        payload.TotalAmount.Should().Be(230m);
        payload.Days.Should().HaveCount(2);
    }

    [Fact]
    public async Task Altegio_GetFinanceDaily_ReturnsIncomeAndExpenseBreakdown()
    {
        var response = await _client.GetAsync("/api/v1/altegio/finance/daily?date=2026-02-12");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<AltegioFinanceDailyResponse>();

        payload.Should().NotBeNull();
        payload!.Date.Should().Be(new DateOnly(2026, 2, 12));
        payload.IncomeTotal.Should().Be(200m);
        payload.ExpenseTotal.Should().Be(35m);
        payload.CashIncome.Should().Be(100m);
        payload.CashExpense.Should().Be(20m);
        payload.TerminalIncome.Should().Be(60m);
        payload.TerminalExpense.Should().Be(10m);
        payload.TransferIncome.Should().Be(40m);
        payload.TransferExpense.Should().Be(5m);
        payload.TransactionsCount.Should().Be(6);
    }

    [Fact]
    public async Task Altegio_GetFinanceDaily_WithInvalidDate_ReturnsBadRequest()
    {
        var response = await _client.GetAsync("/api/v1/altegio/finance/daily?date=12-02-2026");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Altegio_GetFinanceTransactions_ReturnsTotalsByCashRegister_AndTransactionsCount()
    {
        var response = await _client.GetAsync("/api/v1/altegio/finance/transactions?date=2026-02-12");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<AltegioFinanceTransactionsResponse>();

        payload.Should().NotBeNull();
        payload!.From.Should().Be(new DateOnly(2026, 2, 12));
        payload.To.Should().Be(new DateOnly(2026, 2, 12));
        payload.TransactionsCount.Should().Be(6);
        payload.Transactions.Should().HaveCount(6);
        payload.Transactions.Should().OnlyContain(x => x.CreatedAt == null);
        payload.Transactions.Should().ContainSingle(x => x.Id == 1 && x.LastChangeDate == new DateTime(2026, 2, 12, 9, 30, 0));
        payload.CashRegisterTotals.Should().HaveCount(3);

        payload.CashRegisterTotals.Should().ContainSingle(x => x.CashRegisterId == 1464652 && x.TotalAmount == 80m && x.TransactionsCount == 2);
        payload.CashRegisterTotals.Should().ContainSingle(x => x.CashRegisterId == 1464653 && x.TotalAmount == 50m && x.TransactionsCount == 2);
        payload.CashRegisterTotals.Should().ContainSingle(x => x.CashRegisterId == 3 && x.TotalAmount == 35m && x.TransactionsCount == 2);
    }

    [Fact]
    public async Task Altegio_GetFinanceTransactions_WithDate_ReturnsOnlyRequestedDay()
    {
        var response = await _client.GetAsync("/api/v1/altegio/finance/transactions?date=2026-02-13");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<AltegioFinanceTransactionsResponse>();

        payload.Should().NotBeNull();
        payload!.From.Should().Be(new DateOnly(2026, 2, 13));
        payload.To.Should().Be(new DateOnly(2026, 2, 13));
        payload.Transactions.Should().HaveCount(1);
        payload.Transactions[0].Id.Should().Be(7);
        payload.Transactions[0].CreatedAt.Should().BeNull();
        payload.Transactions[0].LastChangeDate.Should().BeNull();
        payload.TransactionsCount.Should().Be(1);
        payload.CashRegisterTotals.Should().ContainSingle(x => x.CashRegisterId == 1464652 && x.TotalAmount == 25m && x.TransactionsCount == 1);
    }

    private HttpClient CreateClientWithMockedAltegio()
    {
        var app = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IAltegioService>();
                services.Configure<AltegioSettings>(options =>
                {
                    options.BaseUrl = "https://api.alteg.io/api/v1/";
                    options.BearerToken = "test-bearer";
                    options.UserToken = "test-user";
                    options.CompanyId = 1;
                });

                services
                    .AddHttpClient<IAltegioService, AltegioService>((serviceProvider, client) =>
                    {
                        var settings = serviceProvider
                            .GetRequiredService<Microsoft.Extensions.Options.IOptions<AltegioSettings>>()
                            .Value;

                        client.BaseAddress = new Uri(settings.BaseUrl);
                        client.DefaultRequestHeaders.Authorization =
                            new AuthenticationHeaderValue("Bearer", $"{settings.BearerToken},{settings.UserToken}");
                        client.DefaultRequestHeaders.Accept.Add(
                            new MediaTypeWithQualityHeaderValue("application/vnd.api.v2+json"));
                    })
                    .ConfigurePrimaryHttpMessageHandler(() => new MockAltegioHttpMessageHandler());
            });
        });

        return app.CreateClient();
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

    private sealed class MockAltegioHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var pathAndQuery = request.RequestUri?.PathAndQuery ?? string.Empty;

            if (pathAndQuery.Contains("/records/1", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(JsonResponse("""
                {
                  "data": [
                    {
                      "id": 501,
                      "date": "2026-02-12T09:00:00",
                      "datetime": "2026-02-12T09:00:00",
                      "visit_attendance": 1,
                      "deleted": false,
                      "client": { "name": "Anna", "surname": "Smith" },
                      "services": [
                        { "title": "Cut", "cost": 60.0, "cost_to_pay": 50.0 },
                        { "title": "Color", "cost": 40.0, "cost_to_pay": 40.0 }
                      ]
                    }
                  ]
                }
                """));
            }

            if (pathAndQuery.Contains("/salary/period/staff/daily/2094973", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(JsonResponse("""
                {
                  "data": [
                    { "date": "2026-02-10", "salary": 100.0 },
                    { "date": "2026-02-11", "salary": 130.0 }
                  ]
                }
                """));
            }

            if (pathAndQuery.Contains("/transactions/1", StringComparison.OrdinalIgnoreCase))
            {
                var query = request.RequestUri?.Query ?? string.Empty;
                if (!query.Contains("start_date=", StringComparison.OrdinalIgnoreCase) ||
                    !query.Contains("end_date=", StringComparison.OrdinalIgnoreCase))
                {
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
                    {
                        Content = new StringContent("{\"data\":[]}", Encoding.UTF8, "application/json")
                    });
                }

                return Task.FromResult(JsonResponse("""
                {
                  "data": [
                                        { "id": 1, "date": "2026-02-12", "datetime": "2026-02-12T09:00:00", "last_change_date": "2026-02-12T09:30:00", "amount": 100.0, "comment": "service", "account": { "id": 1464652, "title": "Cash", "is_cash": true } },
                                        { "id": 2, "date": "2026-02-12", "datetime": "2026-02-12T10:00:00", "amount": 60.0, "comment": "service", "account": { "id": 1464653, "title": "Terminal", "is_cash": false } },
                                        { "id": 3, "date": "2026-02-12", "datetime": "2026-02-12T11:00:00", "amount": 40.0, "comment": "#перевод", "account": { "id": 3, "title": "Transfer", "is_cash": false } },
                                        { "id": 4, "date": "2026-02-12", "datetime": "2026-02-12T12:00:00", "amount": -20.0, "comment": "expense", "account": { "id": 1464652, "title": "Cash", "is_cash": true } },
                                        { "id": 5, "date": "2026-02-12", "datetime": "2026-02-12T13:00:00", "amount": -10.0, "comment": "expense", "account": { "id": 1464653, "title": "Terminal", "is_cash": false } },
                                        { "id": 6, "date": "2026-02-12", "datetime": "2026-02-12T14:00:00", "amount": -5.0, "comment": "#перевод", "account": { "id": 3, "title": "Transfer", "is_cash": false } },
                                        { "id": 7, "date": "2026-02-13", "datetime": "2026-02-13T10:00:00", "amount": 25.0, "comment": "next day", "account": { "id": 1464652, "title": "Cash", "is_cash": true } },
                                        { "id": 8, "date": "2026-02-11", "datetime": "2026-02-11T10:00:00", "amount": 15.0, "comment": "previous day", "account": { "id": 1464653, "title": "Terminal", "is_cash": false } }
                  ]
                }
                """));
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("{\"data\":[]}", Encoding.UTF8, "application/json")
            });
        }

        private static HttpResponseMessage JsonResponse(string body)
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
        }
    }
}
