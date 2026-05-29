namespace CashDash.Api.Models;

public record RouteRequest(
    double AmountSEK,
    string DestinationCountry,
    string TargetCurrency
);

public record RouteDetails(
    string Name,
    string Description,
    int TimeInSeconds,
    double FeeSEK
);

public record RouteResponse(
    string TargetCountry,
    double AmountSEK,
    List<RouteDetails> Routes,
    string SavingsMessage
);