using System.Globalization;
using System.Text.Json;
using Infrastructure.Integrations.Altegio;
using Infrastructure.Integrations.Altegio.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Services;

public sealed class AltegioService : IAltegioService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly AltegioSettings _settings;
    private readonly ILogger<AltegioService> _logger;

    public AltegioService(HttpClient httpClient, IOptions<AltegioSettings> settings, ILogger<AltegioService> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<AltegioScheduleItem>> GetEmployeeScheduleAsync(int employeeId, DateOnly date, CancellationToken cancellationToken = default)
    {
        var query = new Dictionary<string, string>
        {
            ["page"] = "1",
            ["count"] = "300",
            ["staff_id"] = employeeId.ToString(CultureInfo.InvariantCulture),
            ["date_from"] = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["date_to"] = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
        };

        var data = await GetEnvelopeAsync<AltegioRecord>($"records/{_settings.CompanyId}", query, cancellationToken);

        return data
            .OrderBy(x => x.DateTime == default ? x.Date : x.DateTime)
            .Select(x => new AltegioScheduleItem(
                x.Id,
                x.DateTime == default ? x.Date : x.DateTime,
                x.VisitAttendance is 1 or -1,
                x.Deleted,
                JoinClientName(x.Client),
                x.Services.Sum(s => s.Cost),
                x.Services.Sum(s => s.CostToPay),
                x.Services.Select(s => s.Title).Where(t => !string.IsNullOrWhiteSpace(t)).Cast<string>().ToList()))
            .ToList();
    }

    public async Task<AltegioSalaryPeriodResult> GetEmployeeSalaryAsync(int employeeId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        var query = new Dictionary<string, string>
        {
            ["date_from"] = from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["date_to"] = to.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
        };

        var entries = await GetRawDataArrayAsync($"company/{_settings.CompanyId}/salary/period/staff/daily/{employeeId}/", query, cancellationToken);
        var days = new List<AltegioSalaryDay>();

        foreach (var entry in entries)
        {
            if (!TryReadDate(entry, out var date) || !TryReadAmount(entry, out var amount))
                continue;

            days.Add(new AltegioSalaryDay(date, amount));
        }

        return new AltegioSalaryPeriodResult(
            employeeId,
            from,
            to,
            days.Sum(x => x.Amount),
            days.OrderBy(x => x.Date).ToList());
    }

    public async Task<AltegioFinanceDailyResult> GetFinanceDailyAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        var transactions = await GetTransactionsAsync(date, date, cancellationToken);

        var incomes = transactions.Where(x => x.Amount > 0m).ToList();
        var expenses = transactions.Where(x => x.Amount < 0m).ToList();

        var cashIncome = incomes.Where(IsCash).Sum(x => x.Amount);
        var cashExpense = expenses.Where(IsCash).Sum(x => Math.Abs(x.Amount));

        var transferIncome = incomes.Where(IsTransfer).Sum(x => x.Amount);
        var transferExpense = expenses.Where(IsTransfer).Sum(x => Math.Abs(x.Amount));

        var terminalIncome = incomes.Where(x => IsTerminal(x)).Sum(x => x.Amount);
        var terminalExpense = expenses.Where(x => IsTerminal(x)).Sum(x => Math.Abs(x.Amount));

        return new AltegioFinanceDailyResult(
            date,
            incomes.Sum(x => x.Amount),
            expenses.Sum(x => Math.Abs(x.Amount)),
            cashIncome,
            cashExpense,
            terminalIncome,
            terminalExpense,
            transferIncome,
            transferExpense,
            transactions.Count);
    }

    public async Task<AltegioFinanceTransactionsResult> GetFinanceTransactionsAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        var transactions = await GetTransactionsAsync(from, to, cancellationToken);

        var result = transactions
            .OrderBy(ResolveTransactionTimestamp)
            .Select(x =>
            {
                var timestamp = ResolveTransactionTimestamp(x);
                return new AltegioFinanceTransaction(
                    x.Id,
                    timestamp,
                    x.Date,
                    x.LastChangeDate,
                    x.Amount,
                    x.Comment,
                    x.Account?.Id,
                    x.Account?.Title,
                    IsCash(x));
            })
            .ToList();

        return new AltegioFinanceTransactionsResult(from, to, result);
    }

    public async Task<IReadOnlyList<AltegioFinanceSourceTransaction>> GetFinanceSourceTransactionsAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        var transactions = await GetTransactionsAsync(from, to, cancellationToken);

        return transactions
            .OrderBy(ResolveTransactionTimestamp)
            .Select(x => new AltegioFinanceSourceTransaction(
                x.Id,
                ResolveTransactionTimestamp(x),
                ToNullableDateTime(x.CreatedAt),
                ToNullableDateTime(x.LastChangeDate),
                x.Amount,
                x.Comment,
                x.Account?.Id,
                x.Account?.Title,
                IsCash(x),
                JsonSerializer.Serialize(x, JsonOptions)))
            .ToList();
    }

    private async Task<List<T>> GetEnvelopeAsync<T>(string path, IReadOnlyDictionary<string, string> query, CancellationToken cancellationToken)
    {
        var request = BuildRequest(path, query);
        var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (path.StartsWith("transactions/", StringComparison.OrdinalIgnoreCase))
            Console.WriteLine(body);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Altegio request failed for {Path}. Status: {StatusCode}. Body: {Body}", path, response.StatusCode, body);
            throw new HttpRequestException($"Altegio request failed with status code {(int)response.StatusCode}.");
        }

        var payload = JsonSerializer.Deserialize<AltegioEnvelope<T>>(body, JsonOptions);
        return payload?.Data ?? [];
    }

    private async Task<List<JsonElement>> GetRawDataArrayAsync(string path, IReadOnlyDictionary<string, string> query, CancellationToken cancellationToken)
    {
        var request = BuildRequest(path, query);
        var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Altegio request failed for {Path}. Status: {StatusCode}. Body: {Body}", path, response.StatusCode, body);
            throw new HttpRequestException($"Altegio request failed with status code {(int)response.StatusCode}.");
        }

        using var document = JsonDocument.Parse(body);
        if (!document.RootElement.TryGetProperty("data", out var dataElement) || dataElement.ValueKind != JsonValueKind.Array)
            return [];

        return dataElement.EnumerateArray().Select(x => x.Clone()).ToList();
    }

    private static HttpRequestMessage BuildRequest(string path, IReadOnlyDictionary<string, string> query)
    {
        var queryString = string.Join("&", query.Select(x => $"{Uri.EscapeDataString(x.Key)}={Uri.EscapeDataString(x.Value)}"));
        var uri = string.IsNullOrWhiteSpace(queryString) ? path : $"{path}?{queryString}";
        return new HttpRequestMessage(HttpMethod.Get, uri);
    }

    private async Task<List<AltegioTransaction>> GetTransactionsAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        const int pageSize = 500;
        var all = new List<AltegioTransaction>();
        var page = 1;

        while (true)
        {
            var query = new Dictionary<string, string>
            {
                ["page"] = page.ToString(CultureInfo.InvariantCulture),
                ["count"] = pageSize.ToString(CultureInfo.InvariantCulture),
                ["start_date"] = from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                ["end_date"] = to.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            };

            var chunk = await GetEnvelopeAsync<AltegioTransaction>($"transactions/{_settings.CompanyId}", query, cancellationToken);
            if (chunk.Count == 0)
                break;

            all.AddRange(chunk.Where(x => IsTransactionInRange(x, from, to)));
            if (chunk.Count < pageSize)
                break;

            page++;
        }

        return all;
    }

    private static bool IsTransactionInRange(AltegioTransaction transaction, DateOnly from, DateOnly to)
    {
        if (!TryResolveTransactionDate(transaction, out var transactionDate))
            return true;

        return transactionDate >= from && transactionDate <= to;
    }

    private static bool TryResolveTransactionDate(AltegioTransaction transaction, out DateOnly date)
    {
        var timestamp = ResolveTransactionTimestamp(transaction);
        if (timestamp == default)
        {
            date = default;
            return false;
        }

        date = DateOnly.FromDateTime(timestamp);
        return true;
    }

    private static DateTime ResolveTransactionTimestamp(AltegioTransaction transaction)
    {
        return transaction.DateTime == default ? transaction.Date : transaction.DateTime;
    }

    private static DateTime? ToNullableDateTime(DateTime value)
    {
        return value == default ? null : value;
    }

    private static bool IsCash(AltegioTransaction transaction)
    {
        return transaction.Account?.IsCash == true || transaction.Account?.Id == 1464652;
    }

    private static bool IsTransfer(AltegioTransaction transaction)
    {
        if (transaction.Account?.Id == 3)
            return true;

        if (!string.IsNullOrWhiteSpace(transaction.Comment) && transaction.Comment.Contains("#перевод", StringComparison.OrdinalIgnoreCase))
            return true;

        if (!string.IsNullOrWhiteSpace(transaction.Account?.Title) && transaction.Account.Title.Contains("перевод", StringComparison.OrdinalIgnoreCase))
            return true;

        if (!string.IsNullOrWhiteSpace(transaction.Account?.Title) && transaction.Account.Title.Contains("transfer", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    private static bool IsTerminal(AltegioTransaction transaction)
    {
        if (IsTransfer(transaction) || IsCash(transaction))
            return false;

        if (transaction.Account?.Id == 1464653)
            return true;

        return transaction.Account is not null;
    }

    private static string? JoinClientName(AltegioClient? client)
    {
        if (client is null)
            return null;

        var parts = new[] { client.Name, client.Surname }
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToArray();

        return parts.Length == 0 ? null : string.Join(" ", parts);
    }

    private static bool TryReadDate(JsonElement element, out DateOnly date)
    {
        foreach (var key in new[] { "date", "day", "work_date" })
        {
            if (!element.TryGetProperty(key, out var value) || value.ValueKind != JsonValueKind.String)
                continue;

            if (DateOnly.TryParse(value.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
                return true;
        }

        date = default;
        return false;
    }

    private static bool TryReadAmount(JsonElement element, out decimal amount)
    {
        foreach (var key in new[] { "salary", "amount", "to_pay", "sum", "total" })
        {
            if (!element.TryGetProperty(key, out var value))
                continue;

            if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out amount))
                return true;

            if (value.ValueKind == JsonValueKind.String && decimal.TryParse(value.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out amount))
                return true;
        }

        amount = 0m;
        return false;
    }
}
