using Api.Models.Responses;
using Infrastructure.Data;
using Infrastructure.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/employees")]
public class EmployeesController : ControllerBase
{
    private readonly ApplicationDbContext _dbContext;

    public EmployeesController(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<EmployeeResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEmployees(
        [FromQuery] EmployeePosition? position,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.Employees.AsNoTracking();

        if (position is not null)
            query = query.Where(e => e.Position == position.Value);

        var employees = await query
            .OrderBy(e => e.LastName)
            .ThenBy(e => e.FirstName)
            .Select(e => new EmployeeResponse(
                e.Id,
                e.FirstName,
                e.LastName,
                (int)e.Position,
                e.IsActive))
            .ToListAsync(cancellationToken);

        return Ok(employees);
    }
}
