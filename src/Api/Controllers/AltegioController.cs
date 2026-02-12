using System.Globalization;
using Api.Models.Responses;
using Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/altegio")]
public class AltegioController : ControllerBase
{
    private const string DateFormat = "yyyy-MM-dd";

    private readonly IAltegioService _altegioService;

    public AltegioController(IAltegioService altegioService)
    {
        _altegioService = altegioService;
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
}
