using CashDash.Api.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;

namespace CashDash.Api.Services;

public class RouteCalculator : IRouteCalculator
{
    private readonly HttpClient _httpClient;
    private const string ExchangeRateApiKey = "";
    private const string CoinGeckoApiKey = "";

    public RouteCalculator(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("CashDashApi/1.0");
    }

    public async Task<RouteResponse> CalculateAsync(RouteRequest request)
    {
        string targetCurrency = request.TargetCurrency.ToUpper();

        double liveFxRate = await FetchFiatRateAsync(targetCurrency);
        var cryptoPrices = await FetchCryptoPricesInSekAsync();

        double solPriceSek = cryptoPrices.GetValueOrDefault("solana", 1500.0);
        double maticPriceSek = cryptoPrices.GetValueOrDefault("polygon", 10.0);
        double ethPriceSek = cryptoPrices.GetValueOrDefault("ethereum", 35000.0);

        var networks = new List<Web3Network>
        {
            new Web3Network("Solana", 0.00005 * solPriceSek, 1, "USDC"),
            new Web3Network("Polygon", 0.02 * maticPriceSek, 3, "USDC"),
            new Web3Network("Ethereum", 0.0015 * ethPriceSek, 15, "USDC")
        };

        var calculatedRoutes = new List<RouteDetails>();

        foreach (var net in networks)
        {
            double exchangeFee = request.AmountSEK * 0.002;
            double totalFeeSek = net.GasFeeSek + exchangeFee;
            double receivedInTargetFiat = (request.AmountSEK - totalFeeSek) * liveFxRate;

            calculatedRoutes.Add(new RouteDetails(
                $"⚡ Web3: {net.Name} Nätverket",
                $"Flöde: SEK ➡️ {net.Stablecoin} ({net.Name}) ➡️ {targetCurrency}. Mottagaren får ca {receivedInTargetFiat:F2} {targetCurrency}.",
                net.TimeInSeconds,
                Math.Round(totalFeeSek, 2)
            ));
        }

        var optimalWeb3Route = calculatedRoutes.OrderBy(r => r.FeeSEK).First();
        optimalWeb3Route = optimalWeb3Route with { Name = $"🏆 OPTIMAL RUTT: {optimalWeb3Route.Name}" };

        double bankFxRate = liveFxRate * 0.975;
        double swiftFee = 150.0 + (request.AmountSEK * 0.025);
        double receivedViaBank = Math.Max(0, (request.AmountSEK - swiftFee) * bankFxRate);

        var swiftRoute = new RouteDetails(
            "🐌 Traditionell SWIFT (Bank)",
            $"Flöde: Bank ➡️ Korrespondentbank ➡️ Lokal Bank. Mottagaren får ca {receivedViaBank:F2} {targetCurrency} (inkl. 2.5% dolt valutapåslag).",
            259200,
            Math.Round(swiftFee, 2)
        );

        double bestWeb3Payout = (request.AmountSEK - optimalWeb3Route.FeeSEK) * liveFxRate;
        double savings = bestWeb3Payout - receivedViaBank;

        string savingsMessage = $"Live-skanning klar! Genom att använda Web3-routing får mottagaren {savings:F2} {targetCurrency} mer på kontot jämfört med den traditionella banken.";

        return new RouteResponse(
            request.DestinationCountry,
            request.AmountSEK,
            new List<RouteDetails> { optimalWeb3Route, swiftRoute },
            savingsMessage
        );
    }

    private async Task<double> FetchFiatRateAsync(string targetCurrency)
    {
        try
        {
            var response = await _httpClient.GetStringAsync($"https://v6.exchangerate-api.com/v6/{ExchangeRateApiKey}/latest/SEK");
            var data = JsonSerializer.Deserialize<JsonElement>(response);

            if (data.GetProperty("conversion_rates").TryGetProperty(targetCurrency, out var rate))
            {
                return rate.GetDouble();
            }
        }
        catch (Exception)
        {
            Console.WriteLine("⚠️ ExchangeRate-API misslyckades, använder fallback.");
        }

        var fallbacks = new Dictionary<string, double> {
            { "MXN", 1.65 }, { "COP", 380.0 }, { "USD", 0.095 }, { "PHP", 5.40 }, { "INR", 7.90 }
        };

        return fallbacks.GetValueOrDefault(targetCurrency, 1.0);
    }

    private async Task<Dictionary<string, double>> FetchCryptoPricesInSekAsync()
    {
        var prices = new Dictionary<string, double>();
        try
        {
            var requestUrl = "https://api.coingecko.com/api/v3/simple/price?ids=solana,polygon,ethereum&vs_currencies=sek";

            using var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
            request.Headers.Add("x-cg-api-key", CoinGeckoApiKey);

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var responseString = await response.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<JsonElement>(responseString);

            if (data.TryGetProperty("solana", out var sol) && sol.TryGetProperty("sek", out var solSek))
                prices.Add("solana", solSek.GetDouble());

            if (data.TryGetProperty("polygon", out var matic) && matic.TryGetProperty("sek", out var maticSek))
                prices.Add("polygon", maticSek.GetDouble());

            if (data.TryGetProperty("ethereum", out var eth) && eth.TryGetProperty("sek", out var ethSek))
                prices.Add("ethereum", ethSek.GetDouble());
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ CoinGecko-API misslyckades ({ex.Message}), använder fallback.");
            prices.Add("solana", 1550.0);
            prices.Add("polygon", 11.50);
            prices.Add("ethereum", 36000.0);
        }

        return prices;
    }
}

public record Web3Network(string Name, double GasFeeSek, int TimeInSeconds, string Stablecoin);