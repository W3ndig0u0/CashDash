using CashDash.Api.Models;

namespace CashDash.Api.Services;

public interface IRouteCalculator
{
    Task<RouteResponse> CalculateAsync(RouteRequest request);
}
