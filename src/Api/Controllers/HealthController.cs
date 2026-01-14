using Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("[controller]")]
public class HealthController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<HealthController> _logger;

    public HealthController(ApplicationDbContext context, ILogger<HealthController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Get()
    {
        var correlationId = HttpContext.Items["CorrelationId"];
        _logger.LogInformation("Health check initiated. CorrelationId: {CorrelationId}", correlationId);

        try
        {
            await _context.Database.CanConnectAsync();
            _logger.LogInformation("Database connection successful. CorrelationId: {CorrelationId}", correlationId);
            return Ok(new { status = "healthy", database = "connected" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database connection failed. CorrelationId: {CorrelationId}", correlationId);
            return StatusCode(503, new { status = "unhealthy", database = "disconnected" });
        }
    }
}
