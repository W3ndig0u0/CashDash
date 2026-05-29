using CashDash.Api.Services;
using Microsoft.AspNetCore.Mvc;
using CashDash.Api.Models;

namespace CashDash.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RouteController : ControllerBase
{
    private readonly IRouteCalculator _routeCalculator;
    public RouteController(IRouteCalculator routeCalculator)
    {
        _routeCalculator = routeCalculator;
    }

    [HttpPost("calculate")]
    public async Task<IActionResult> CalculateRoutes([FromBody] RouteRequest request)
    {
        var response = await _routeCalculator.CalculateAsync(request);
        return Ok(response);
    }
}