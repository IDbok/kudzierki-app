using System.Globalization;
using System.Security.Claims;
using Api.Models.Requests;
using Api.Models.Responses;
using Infrastructure.Data;
using Infrastructure.Entities.Schedule;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/schedule")]
public class ScheduleController : ControllerBase
{
    private const string DateFormat = "yyyy-MM-dd";

    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<ScheduleController> _logger;

    public ScheduleController(ApplicationDbContext dbContext, ILogger<ScheduleController> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    [HttpGet("workdays")]
    [ProducesResponseType(typeof(IReadOnlyList<WorkDayResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetWorkDays([FromQuery] string from, [FromQuery] string to, CancellationToken cancellationToken)
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

        var workDays = await _dbContext.WorkDays
            .AsNoTracking()
            .Include(wd => wd.Assignments)
            .Where(wd => wd.Date >= fromDate && wd.Date <= toDate)
            .OrderBy(wd => wd.Date)
            .ToListAsync(cancellationToken);

        return Ok(workDays.Select(Map).ToList());
    }

    [HttpPut("workdays/{date}")]
    [ProducesResponseType(typeof(WorkDayResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpsertWorkDay([FromRoute] string date, [FromBody] UpsertWorkDayRequest? request, CancellationToken cancellationToken)
    {
        _ = request;

        if (!TryParseDateOnly(date, out var dayDate))
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid route parameter",
                Detail = $"Route parameter 'date' must be in format {DateFormat}."
            });
        }

        var userId = GetCurrentUserId();

        var existing = await _dbContext.WorkDays
            .Include(wd => wd.Assignments)
            .SingleOrDefaultAsync(wd => wd.Date == dayDate, cancellationToken);

        if (existing is null)
        {
            _dbContext.WorkDays.Add(new WorkDay
            {
                Date = dayDate,
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedByUserId = userId
            });

            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex)
            {
                _logger.LogWarning(ex, "UpsertWorkDay hit a conflict for date {Date}", dayDate);
            }

            existing = await _dbContext.WorkDays
                .Include(wd => wd.Assignments)
                .SingleAsync(wd => wd.Date == dayDate, cancellationToken);
        }

        return Ok(Map(existing));
    }

    [HttpPost("workdays/{date}/assignments")]
    [ProducesResponseType(typeof(WorkDayAssignmentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateAssignment(
        [FromRoute] string date,
        [FromBody] CreateWorkDayAssignmentRequest request,
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

        if (request.EmployeeId == Guid.Empty)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Validation error",
                Detail = "EmployeeId is required."
            });
        }

        if (!Enum.IsDefined(typeof(ShiftPortionType), request.PortionType))
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Validation error",
                Detail = "PortionType has an invalid value."
            });
        }

        var employeeExists = await _dbContext.Employees
            .AsNoTracking()
            .AnyAsync(e => e.Id == request.EmployeeId, cancellationToken);

        if (!employeeExists)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Not found",
                Detail = "Employee not found."
            });
        }

        var userId = GetCurrentUserId();

        var workDay = await _dbContext.WorkDays
            .SingleOrDefaultAsync(wd => wd.Date == dayDate, cancellationToken);

        if (workDay is null)
        {
            workDay = new WorkDay
            {
                Date = dayDate,
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedByUserId = userId
            };

            _dbContext.WorkDays.Add(workDay);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        var assignment = new WorkDayAssignment
        {
            WorkDayId = workDay.Id,
            EmployeeId = request.EmployeeId,
            PortionType = (ShiftPortionType)request.PortionType,
            Portion = request.Portion,
            Note = request.Note,
            Worked = request.Worked,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedByUserId = userId
        };

        _dbContext.WorkDayAssignments.Add(assignment);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(Map(assignment));
    }

    [HttpPatch("assignments/{assignmentId:guid}")]
    [ProducesResponseType(typeof(WorkDayAssignmentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateAssignment(
        [FromRoute] Guid assignmentId,
        [FromBody] UpdateWorkDayAssignmentRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var assignment = await _dbContext.WorkDayAssignments
            .SingleOrDefaultAsync(a => a.Id == assignmentId, cancellationToken);

        if (assignment is null)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Not found",
                Detail = "Assignment not found."
            });
        }

        if (request.PortionType is not null && !Enum.IsDefined(typeof(ShiftPortionType), request.PortionType.Value))
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Validation error",
                Detail = "PortionType has an invalid value."
            });
        }

        if (request.Portion is not null)
            assignment.Portion = request.Portion.Value;

        if (request.PortionType is not null)
            assignment.PortionType = (ShiftPortionType)request.PortionType.Value;

        if (request.Note is not null)
            assignment.Note = request.Note;

        if (request.Worked is not null)
            assignment.Worked = request.Worked;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(Map(assignment));
    }

    [HttpDelete("assignments/{assignmentId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAssignment([FromRoute] Guid assignmentId, CancellationToken cancellationToken)
    {
        var assignment = await _dbContext.WorkDayAssignments
            .SingleOrDefaultAsync(a => a.Id == assignmentId, cancellationToken);

        if (assignment is null)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Not found",
                Detail = "Assignment not found."
            });
        }

        _dbContext.WorkDayAssignments.Remove(assignment);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok();
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
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

    private static WorkDayResponse Map(WorkDay workDay)
    {
        return new WorkDayResponse(
            workDay.Date,
            workDay.Assignments.Select(Map).ToList());
    }

    private static WorkDayAssignmentResponse Map(WorkDayAssignment assignment)
    {
        return new WorkDayAssignmentResponse(
            assignment.Id,
            assignment.EmployeeId,
            (int)assignment.PortionType,
            assignment.Portion,
            assignment.Worked,
            assignment.Note);
    }
}
