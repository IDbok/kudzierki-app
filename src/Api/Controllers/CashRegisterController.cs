using System.Globalization;
using Api.Models.Requests;
using Api.Models.Responses;
using Infrastructure.Data;
using Infrastructure.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

[ApiController]
//[Authorize]
[Route("api/v1/cash-register")]
public class CashRegisterController : ControllerBase
{
    private const string DateFormat = "yyyy-MM-dd";

    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<CashRegisterController> _logger;

    public CashRegisterController(ApplicationDbContext dbContext, ILogger<CashRegisterController> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    [HttpGet("closings")]
    [ProducesResponseType(typeof(IReadOnlyList<CashRegisterClosingResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetClosings([FromQuery] string from, [FromQuery] string to, CancellationToken cancellationToken)
    {
        if (!TryParseDateOnly(from, out var fromDate))
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid query parameter",
                Detail = $"Query parameter 'from' must be in format {DateFormat}."
            });
        }

        if (!TryParseDateOnly(to, out var toDate))
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid query parameter",
                Detail = $"Query parameter 'to' must be in format {DateFormat}."
            });
        }

        if (toDate < fromDate)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid query parameter",
                Detail = "Query parameter 'to' must be greater than or equal to 'from'."
            });
        }

        var closings = await _dbContext.CashRegisterClosings
            .AsNoTracking()
            .Where(c => c.Date >= fromDate && c.Date <= toDate)
            .OrderBy(c => c.Date)
            .ToListAsync(cancellationToken);

        return Ok(closings.Select(Map).ToList());
    }

    [HttpGet("closings/{date}")]
    [ProducesResponseType(typeof(CashRegisterClosingResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetClosing([FromRoute] string date, CancellationToken cancellationToken)
    {
        if (!TryParseDateOnly(date, out var dayDate))
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid route parameter",
                Detail = $"Route parameter 'date' must be in format {DateFormat}."
            });
        }

        var closing = await _dbContext.CashRegisterClosings
            .AsNoTracking()
            .SingleOrDefaultAsync(c => c.Date == dayDate, cancellationToken);

        if (closing is null)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Not found",
                Detail = "Cash register closing not found."
            });
        }

        return Ok(Map(closing));
    }

    [HttpPut("closings/{date}")]
    [ProducesResponseType(typeof(CashRegisterClosingResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpsertClosing(
        [FromRoute] string date,
        [FromBody] UpsertCashRegisterClosingRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        if (!TryParseDateOnly(date, out var dayDate))
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid route parameter",
                Detail = $"Route parameter 'date' must be in format {DateFormat}."
            });
        }

        var closing = await _dbContext.CashRegisterClosings
            .SingleOrDefaultAsync(c => c.Date == dayDate, cancellationToken);

        if (closing is null)
        {
            closing = new CashRegisterClosing
            {
                Date = dayDate,
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedByUserId = GetCurrentUserId()
            };
            _dbContext.CashRegisterClosings.Add(closing);
        }

        closing.CashBalanceFact = request.CashBalanceFact;
        closing.TerminalIncomeFact = request.TerminalIncomeFact;
        closing.Comment = request.Comment;

        closing.CashIncomeAltegio = request.CashIncomeAltegio ?? 0m;
        closing.TransferIncomeAltegio = request.TransferIncomeAltegio ?? 0m;
        closing.TerminalIncomeAltegio = request.TerminalIncomeAltegio ?? 0m;
        closing.CashSpendingAdmin = request.CashSpendingAdmin ?? 0m;
        closing.InCashTransfer = request.InCashTransfer ?? 0m;
        closing.OutCashTransfer = request.OutCashTransfer ?? 0m;

        var previousClosing = await _dbContext.CashRegisterClosings
            .AsNoTracking()
            .Where(c => c.Date < dayDate)
            .OrderByDescending(c => c.Date)
            .FirstOrDefaultAsync(cancellationToken);

        if (previousClosing is not null)
        {
            closing.DayBefore = previousClosing.Date;
            closing.CashBalanceDayBefore = previousClosing.CashBalanceFact;
        }
        else
        {
            closing.DayBefore = null;
            closing.CashBalanceDayBefore = 0m;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Cash register closing saved for {Date}", dayDate);

        return Ok(Map(closing));
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.Parse(userIdClaim!);
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

    private static CashRegisterClosingResponse Map(CashRegisterClosing closing)
    {
        var cashIncomeFact = closing.CashBalanceFact
            - closing.CashBalanceDayBefore
            + closing.CashSpendingAdmin
            - closing.InCashTransfer
            + closing.OutCashTransfer;

        var cashIncomeDifference = closing.CashIncomeAltegio - cashIncomeFact;
        var terminalIncomeDifference = closing.TerminalIncomeAltegio - closing.TerminalIncomeFact;

        return new CashRegisterClosingResponse(
            closing.Date,
            closing.CashBalanceFact,
            closing.TerminalIncomeFact,
            closing.DayBefore,
            closing.CashBalanceDayBefore,
            cashIncomeFact,
            closing.CashIncomeAltegio,
            cashIncomeDifference,
            closing.TerminalIncomeAltegio,
            terminalIncomeDifference,
            closing.TransferIncomeAltegio,
            closing.CashSpendingAdmin,
            closing.InCashTransfer,
            closing.OutCashTransfer,
            Math.Abs(cashIncomeDifference) < 5m,
            terminalIncomeDifference == 0m,
            closing.Comment);
    }
}
