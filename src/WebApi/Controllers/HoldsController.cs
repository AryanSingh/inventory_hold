using Contracts;
using Domain.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[EnableRateLimiting("fixed")]
public class HoldsController : ControllerBase
{
    private readonly HoldService _holdService;
    private readonly ILogger<HoldsController> _logger;

    public HoldsController(HoldService holdService, ILogger<HoldsController> logger)
    {
        _holdService = holdService;
        _logger = logger;
    }

    [HttpPost]
    [EnableRateLimiting("sliding")]
    public async Task<IActionResult> CreateHold([FromBody] CreateHoldRequest request)
    {
        // Input validation
        if (request.Items == null || request.Items.Count == 0)
        {
            return BadRequest(new ProblemDetails
            {
                Status = 400,
                Title = "Validation Error",
                Detail = "At least one item is required."
            });
        }

        if (request.Items.Count > 50)
        {
            return BadRequest(new ProblemDetails
            {
                Status = 400,
                Title = "Validation Error",
                Detail = "Maximum 50 items per hold request."
            });
        }

        foreach (var item in request.Items)
        {
            if (string.IsNullOrWhiteSpace(item.ProductId))
            {
                return BadRequest(new ProblemDetails
                {
                    Status = 400,
                    Title = "Validation Error",
                    Detail = "ProductId is required."
                });
            }

            // Validate ProductId format (UUID)
            if (!Guid.TryParse(item.ProductId, out _))
            {
                return BadRequest(new ProblemDetails
                {
                    Status = 400,
                    Title = "Validation Error",
                    Detail = $"Invalid ProductId format: {item.ProductId}"
                });
            }

            if (item.Quantity <= 0)
            {
                return BadRequest(new ProblemDetails
                {
                    Status = 400,
                    Title = "Validation Error",
                    Detail = $"Quantity must be greater than 0 for product {item.ProductId}."
                });
            }

            if (item.Quantity > 1000)
            {
                return BadRequest(new ProblemDetails
                {
                    Status = 400,
                    Title = "Validation Error",
                    Detail = $"Quantity cannot exceed 1000 for product {item.ProductId}."
                });
            }
        }

        // Validate DurationMinutes
        if (request.DurationMinutes.HasValue)
        {
            if (request.DurationMinutes.Value < 1 || request.DurationMinutes.Value > 1440)
            {
                return BadRequest(new ProblemDetails
                {
                    Status = 400,
                    Title = "Validation Error",
                    Detail = "DurationMinutes must be between 1 and 1440 (24 hours)."
                });
            }
        }

        // Validate HoldId format if provided
        if (!string.IsNullOrEmpty(request.HoldId) && !Guid.TryParse(request.HoldId, out _))
        {
            return BadRequest(new ProblemDetails
            {
                Status = 400,
                Title = "Validation Error",
                Detail = "Invalid HoldId format."
            });
        }

        try
        {
            _logger.LogInformation("Creating hold with {ItemCount} items", request.Items.Count);
            var hold = await _holdService.CreateHoldAsync(request);
            _logger.LogInformation("Created hold {HoldId}", hold.HoldId);
            return CreatedAtAction(nameof(GetHold), new { holdId = hold.HoldId }, hold);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Validation error creating hold");
            return BadRequest(new ProblemDetails
            {
                Status = 400,
                Title = "Validation Error",
                Detail = ex.Message
            });
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Product not found");
            return NotFound(new ProblemDetails
            {
                Status = 404,
                Title = "Not Found",
                Detail = ex.Message
            });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Insufficient stock");
            return Conflict(new ProblemDetails
            {
                Status = 409,
                Title = "Insufficient Stock",
                Detail = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error creating hold");
            return StatusCode(500, new ProblemDetails
            {
                Status = 500,
                Title = "Internal Server Error",
                Detail = "An unexpected error occurred."
            });
        }
    }

    [HttpGet("{holdId}")]
    public async Task<IActionResult> GetHold(string holdId)
    {
        var hold = await _holdService.GetHoldAsync(holdId);
        if (hold == null)
        {
            return NotFound(new ProblemDetails
            {
                Status = 404,
                Title = "Not Found",
                Detail = $"Hold {holdId} not found."
            });
        }
        return Ok(hold);
    }

    [HttpDelete("{holdId}")]
    public async Task<IActionResult> ReleaseHold(string holdId)
    {
        try
        {
            var result = await _holdService.ReleaseHoldAsync(holdId);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ProblemDetails
            {
                Status = 404,
                Title = "Not Found",
                Detail = ex.Message
            });
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(410, new ProblemDetails
            {
                Status = 410,
                Title = "Gone",
                Detail = ex.Message
            });
        }
    }
}
