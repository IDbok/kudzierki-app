using System.Globalization;
using Api.Models.Responses;
using Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
//[Authorize]
[Route("api/v1/altegio")]
public class AltegioController : ControllerBase
{
    private const string DateFormat = "yyyy-MM-dd";

    private readonly IAltegioService _altegioService;
    private readonly IAltegioTransactionIngestionService _ingestionService;

    public AltegioController(IAltegioService altegioService, IAltegioTransactionIngestionService ingestionService)
    {
        _altegioService = altegioService;
        _ingestionService = ingestionService;
    }

    [HttpGet("employees/{employeeId:int}/schedule")]
    [ProducesResponseType(typeof(AltegioEmployeeScheduleResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetEmployeeSchedule([FromRoute] int employeeId, [FromQuery] string date, CancellationToken cancellationToken)
    {
        if (employeeId <= 0)
            return BadRequest(CreateBadRequest("Route parameter 'employeeId' must be greater than zero."));

        if (!TryParseDateOnly(date, out var day))
            return BadRequest(CreateBadRequest($"Query parameter 'date' must be in format {DateFormat}."));

        var records = await _altegioService.GetEmployeeScheduleAsync(employeeId, day, cancellationToken);
        return Ok(new AltegioEmployeeScheduleResponse(
            employeeId,
            day,
            records.Select(x => new AltegioScheduleItemResponse(
                x.RecordId,
                x.StartAt,
                x.IsVisited,
                x.IsDeleted,
                x.ClientName,
                x.TotalCost,
                x.TotalCostToPay,
                x.Services)).ToList()));
    }

    [HttpGet("employees/{employeeId:int}/salary")]
    [ProducesResponseType(typeof(AltegioSalaryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetEmployeeSalary([FromRoute] int employeeId, [FromQuery] string from, [FromQuery] string to, CancellationToken cancellationToken)
    {
        if (employeeId <= 0)
            return BadRequest(CreateBadRequest("Route parameter 'employeeId' must be greater than zero."));

        if (!TryParseDateOnly(from, out var fromDate))
            return BadRequest(CreateBadRequest($"Query parameter 'from' must be in format {DateFormat}."));

        if (!TryParseDateOnly(to, out var toDate))
            return BadRequest(CreateBadRequest($"Query parameter 'to' must be in format {DateFormat}."));

        if (toDate < fromDate)
            return BadRequest(CreateBadRequest("Query parameter 'to' must be greater than or equal to 'from'."));

        var salary = await _altegioService.GetEmployeeSalaryAsync(employeeId, fromDate, toDate, cancellationToken);
        return Ok(new AltegioSalaryResponse(
            salary.EmployeeId,
            salary.From,
            salary.To,
            salary.TotalAmount,
            salary.Days.Select(x => new AltegioSalaryDayResponse(x.Date, x.Amount)).ToList()));
    }

    [HttpGet("finance/daily")]
    [ProducesResponseType(typeof(AltegioFinanceDailyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetFinanceDaily([FromQuery] string date, CancellationToken cancellationToken)
    {
        if (!TryParseDateOnly(date, out var day))
            return BadRequest(CreateBadRequest($"Query parameter 'date' must be in format {DateFormat}."));

        var result = await _altegioService.GetFinanceDailyAsync(day, cancellationToken);

        return Ok(new AltegioFinanceDailyResponse(
            result.Date,
            result.IncomeTotal,
            result.ExpenseTotal,
            result.CashIncome,
            result.CashExpense,
            result.TerminalIncome,
            result.TerminalExpense,
            result.TransferIncome,
            result.TransferExpense,
            result.TransactionsCount));
    }

    [HttpGet("finance/transactions")]
    [ProducesResponseType(typeof(AltegioFinanceTransactionsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetFinanceTransactions(
        [FromQuery] string? date,
        [FromQuery] string? from,
        [FromQuery] string? to,
        CancellationToken cancellationToken)
    {
        if (!TryResolveFinanceRange(date, from, to, out var fromDate, out var toDate, out var error))
            return BadRequest(CreateBadRequest(error));

        var result = await _altegioService.GetFinanceTransactionsAsync(fromDate, toDate, cancellationToken);
        var transactions = result.Transactions
            .Select(x => new AltegioFinanceTransactionItemResponse(
                x.Id,
                x.DateTime,
                x.CreatedAt,
                x.LastChangeDate,
                x.Amount,
                x.Comment,
                x.AccountId,
                x.AccountTitle,
                x.IsCash))
            .ToList();

        var cashRegisterTotals = result.Transactions
            .GroupBy(x => new { x.AccountId, x.AccountTitle })
            .Select(x => new AltegioFinanceCashRegisterTotalResponse(
                x.Key.AccountId,
                x.Key.AccountTitle,
                x.Sum(t => t.Amount),
                x.Count()))
            .OrderByDescending(x => x.TotalAmount)
            .ToList();

        return Ok(new AltegioFinanceTransactionsResponse(
            result.From,
            result.To,
            cashRegisterTotals,
            transactions.Count,
            transactions));
    }

    [HttpPost("finance/transactions/sync")]
    [ProducesResponseType(typeof(AltegioFinanceTransactionsSyncResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SyncFinanceTransactions(
        [FromQuery] string? date,
        [FromQuery] string? from,
        [FromQuery] string? to,
        CancellationToken cancellationToken)
    {
        if (!TryResolveFinanceRange(date, from, to, out var fromDate, out var toDate, out var error))
            return BadRequest(CreateBadRequest(error));

        var result = await _ingestionService.IngestFinanceTransactionsAsync(fromDate, toDate, cancellationToken);

        return Ok(new AltegioFinanceTransactionsSyncResponse(
            result.From,
            result.To,
            result.FetchedCount,
            result.DistinctExternalIdsCount,
            result.RawInsertedCount,
            result.SnapshotInsertedCount,
            result.SnapshotUpdatedCount));
    }

    private static ProblemDetails CreateBadRequest(string detail)
    {
        return new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Invalid query parameter",
            Detail = detail
        };
    }

    private static bool TryParseDateOnly(string value, out DateOnly date)
    {
        return DateOnly.TryParseExact(
            value,
            DateFormat,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out date);
    }

    private static bool TryResolveFinanceRange(
        string? date,
        string? from,
        string? to,
        out DateOnly fromDate,
        out DateOnly toDate,
        out string error)
    {
        var hasDate = !string.IsNullOrWhiteSpace(date);
        var hasFrom = !string.IsNullOrWhiteSpace(from);
        var hasTo = !string.IsNullOrWhiteSpace(to);

        if (hasDate && (hasFrom || hasTo))
        {
            fromDate = default;
            toDate = default;
            error = "Use either 'date' or 'from' and 'to', but not both.";
            return false;
        }

        if (hasDate)
        {
            if (!TryParseDateOnly(date!, out var day))
            {
                fromDate = default;
                toDate = default;
                error = $"Query parameter 'date' must be in format {DateFormat}.";
                return false;
            }

            fromDate = day;
            toDate = day;
            error = string.Empty;
            return true;
        }

        if (!hasFrom || !hasTo)
        {
            fromDate = default;
            toDate = default;
            error = $"Specify either 'date' or both 'from' and 'to' in format {DateFormat}.";
            return false;
        }

        if (!TryParseDateOnly(from!, out fromDate))
        {
            toDate = default;
            error = $"Query parameter 'from' must be in format {DateFormat}.";
            return false;
        }

        if (!TryParseDateOnly(to!, out toDate))
        {
            error = $"Query parameter 'to' must be in format {DateFormat}.";
            return false;
        }

        if (toDate < fromDate)
        {
            error = "Query parameter 'to' must be greater than or equal to 'from'.";
            return false;
        }

        error = string.Empty;
        return true;
    }
}
