using CashDash.Api.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace CashDash.Api.Services;

public class ApiSettings
{
    public string ExchangeRateApiKey { get; set; } = string.Empty;
    public string CoinGeckoApiKey { get; set; } = string.Empty;
    public string EtherscanApiKey { get; set; } = string.Empty;
}

public class RouteCalculator : IRouteCalculator
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<RouteCalculator> _logger;
    private readonly IMemoryCache _cache;
    private readonly ApiSettings _apiSettings;

    public RouteCalculator(
        HttpClient httpClient,
        ILogger<RouteCalculator> logger,
        IMemoryCache cache,
        IOptions<ApiSettings> apiSettings)
    {
        _httpClient = httpClient;
        _logger = logger;
        _cache = cache;
        _apiSettings = apiSettings.Value;

        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("CashDashApi/1.0");
    }

    public async Task<RouteResponse> CalculateAsync(RouteRequest request)
    {
        string targetCurrency = request.TargetCurrency.ToUpper();

        var fiatTask = FetchFiatRateAsync(targetCurrency);
        var cryptoTask = FetchCryptoPricesInSekAsync();
        var krakenTask = FetchKrakenTakerFeeAsync();
        var withdrawalFeesTask = FetchLiveWithdrawalFeesUsdAsync();
        var wiseTask = FetchWiseFeeSekAsync(request.AmountSEK, targetCurrency);

        var ethGasTask = FetchEvmGasPriceGweiAsync("EthGas", $"https://api.etherscan.io/api?module=gastracker&action=gasoracle&apikey={_apiSettings.EtherscanApiKey}", 25.0);
        var baseGasTask = FetchEvmGasPriceGweiAsync("BaseGas", $"https://api.basescan.org/api?module=gastracker&action=gasoracle&apikey={_apiSettings.EtherscanApiKey}", 0.1);
        var arbGasTask = FetchEvmGasPriceGweiAsync("ArbGas", $"https://api.arbiscan.io/api?module=gastracker&action=gasoracle&apikey={_apiSettings.EtherscanApiKey}", 0.1);
        var polyGasTask = FetchEvmGasPriceGweiAsync("PolyGas", $"https://api.polygonscan.com/api?module=gastracker&action=gasoracle&apikey={_apiSettings.EtherscanApiKey}", 30.0);

        await Task.WhenAll(fiatTask, cryptoTask, krakenTask, withdrawalFeesTask, wiseTask, ethGasTask, baseGasTask, arbGasTask, polyGasTask);

        double liveFxRate = fiatTask.Result;
        var cryptoPrices = cryptoTask.Result;
        double krakenTakerFeePct = krakenTask.Result;
        var liveWithdrawalFeesUsd = withdrawalFeesTask.Result;
        double wiseFeeSek = wiseTask.Result;

        double ethGasGwei = ethGasTask.Result;
        double baseGasGwei = baseGasTask.Result;
        double arbGasGwei = arbGasTask.Result;
        double polyGasGwei = polyGasTask.Result;

        double solPriceSek = cryptoPrices.GetValueOrDefault("solana", 1500.0);
        double maticPriceSek = cryptoPrices.GetValueOrDefault("polygon", 10.0);
        double ethPriceSek = cryptoPrices.GetValueOrDefault("ethereum", 35000.0);
        double tronPriceSek = cryptoPrices.GetValueOrDefault("tron", 1.20);

        double erc20GasLimit = 65000;

        double ethGasFeeSek = (erc20GasLimit * ethGasGwei * 0.000000001) * ethPriceSek;
        double baseGasFeeSek = (erc20GasLimit * baseGasGwei * 0.000000001) * ethPriceSek;
        double arbGasFeeSek = (erc20GasLimit * arbGasGwei * 0.000000001) * ethPriceSek;
        double polyGasFeeSek = (erc20GasLimit * polyGasGwei * 0.000000001) * maticPriceSek;
        double solGasFeeSek = 0.00005 * solPriceSek;
        double tronGasFeeSek = 1.5 * tronPriceSek;

        List<Web3Network> networks = [
            new("Solana", solGasFeeSek, 1, "USDC"),
            new("Base", baseGasFeeSek, 2, "USDC"),
            new("Arbitrum", arbGasFeeSek, 2, "USDC"),
            new("Polygon", polyGasFeeSek, 3, "USDC"),
            new("Tron", tronGasFeeSek, 3, "USDT"),
            new("Ethereum", ethGasFeeSek, 15, "USDC")
        ];

        var allRoutes = new List<RouteDetails>();

        foreach (var net in networks)
        {
            double tradingFeeSek = request.AmountSEK * krakenTakerFeePct;
            double withdrawalFeeUsd = liveWithdrawalFeesUsd.GetValueOrDefault(net.Name, 1.0);
            double withdrawalFeeSek = withdrawalFeeUsd * liveFxRate;

            double totalFeeSek = tradingFeeSek + withdrawalFeeSek + net.GasFeeSek;
            double receivedInTargetFiat = (request.AmountSEK - totalFeeSek) * liveFxRate;

            allRoutes.Add(new RouteDetails(
                net.Name,
                $"Mottagaren får {receivedInTargetFiat:F2} {targetCurrency}",
                net.TimeInSeconds,
                Math.Round(totalFeeSek, 2)
            ));
        }

        double bankSwiftFeeSek = 50.0;
        double bankFxSpread = 0.015;
        double bankRealFxRate = liveFxRate * (1 - bankFxSpread);
        double amountAfterSwiftFee = request.AmountSEK - bankSwiftFeeSek;
        double receivedViaBank = Math.Max(0, amountAfterSwiftFee * bankRealFxRate);
        double totalBankFeeSek = bankSwiftFeeSek + (request.AmountSEK * bankFxSpread);

        var bankRoute = new RouteDetails(
            "Traditionell Bank (SWIFT)",
            $"Mottagaren får {receivedViaBank:F2} {targetCurrency}",
            259200,
            Math.Round(totalBankFeeSek, 2)
        );
        allRoutes.Add(bankRoute);

        double receivedViaWise = Math.Max(0, (request.AmountSEK - wiseFeeSek) * liveFxRate);
        var wiseRoute = new RouteDetails(
            "Wise",
            $"Mottagaren får {receivedViaWise:F2} {targetCurrency}",
            1800,
            Math.Round(wiseFeeSek, 2)
        );
        allRoutes.Add(wiseRoute);

        var sortedRoutes = allRoutes.OrderBy(r => r.FeeSEK).ToList();

        var bestRoute = sortedRoutes.First();
        double maximumPossiblePayout = (request.AmountSEK - bestRoute.FeeSEK) * liveFxRate;
        if (bestRoute.Name.Contains("Bank"))
        {
            maximumPossiblePayout = receivedViaBank;
        }

        double savings = maximumPossiblePayout - receivedViaBank;
        string savingsMessage = savings > 0
            ? $"Genom att välja optimal rutt får mottagaren {savings:F2} {targetCurrency} mer på kontot jämfört med banken!"
            : "Traditionell bank är ovanligt nog det billigaste alternativet för denna överföring.";

        return new RouteResponse(
            request.DestinationCountry,
            request.AmountSEK,
            sortedRoutes,
            savingsMessage
        );
    }

    private async Task<Dictionary<string, double>> FetchLiveWithdrawalFeesUsdAsync()
    {
        return await _cache.GetOrCreateAsync("LiveWithdrawalFeesUsd", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
            var fees = new Dictionary<string, double>();

            try
            {
                var response = await _httpClient.GetStringAsync("https://api.kucoin.com/api/v3/currencies/USDC");
                using var doc = JsonDocument.Parse(response);
                var root = doc.RootElement;

                if (root.GetProperty("code").GetString() == "200000")
                {
                    var chains = root.GetProperty("data").GetProperty("chains");
                    foreach (var chain in chains.EnumerateArray())
                    {
                        string chainName = chain.GetProperty("chainName").GetString()?.ToUpper() ?? "";
                        string feeStr = chain.GetProperty("withdrawalMinFee").GetString() ?? "0";

                        if (double.TryParse(feeStr, System.Globalization.CultureInfo.InvariantCulture, out double feeUsd))
                        {
                            if (chainName.Contains("ERC20")) fees["Ethereum"] = feeUsd;
                            else if (chainName.Contains("TRC20")) fees["Tron"] = feeUsd;
                            else if (chainName.Contains("SOL")) fees["Solana"] = feeUsd;
                            else if (chainName.Contains("MATIC")) fees["Polygon"] = feeUsd;
                            else if (chainName.Contains("ARBITRUM")) fees["Arbitrum"] = feeUsd;
                            else if (chainName.Contains("BASE")) fees["Base"] = feeUsd;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "KuCoin API misslyckades. Använder fallback-uttagsavgifter.");
            }

            if (!fees.ContainsKey("Ethereum")) fees["Ethereum"] = 5.0;
            if (!fees.ContainsKey("Tron")) fees["Tron"] = 1.0;
            if (!fees.ContainsKey("Solana")) fees["Solana"] = 0.5;
            if (!fees.ContainsKey("Polygon")) fees["Polygon"] = 0.5;
            if (!fees.ContainsKey("Arbitrum")) fees["Arbitrum"] = 0.5;
            if (!fees.ContainsKey("Base")) fees["Base"] = 0.5;

            return fees;
        });
    }

    private async Task<double> FetchEvmGasPriceGweiAsync(string cacheKey, string apiUrl, double fallbackGwei)
    {
        double? gwei = await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(15);

            try
            {
                var response = await _httpClient.GetStringAsync(apiUrl);
                using var doc = JsonDocument.Parse(response);
                var root = doc.RootElement;

                if (root.GetProperty("status").GetString() == "1")
                {
                    string proposeGas = root.GetProperty("result").GetProperty("ProposeGasPrice").GetString() ?? fallbackGwei.ToString();

                    if (double.TryParse(proposeGas, System.Globalization.CultureInfo.InvariantCulture, out double gasInGwei))
                    {
                        return gasInGwei;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Gas Oracle API misslyckades för {CacheKey}.", cacheKey);
            }

            return fallbackGwei;
        });

        return gwei ?? fallbackGwei;
    }

    private async Task<double> FetchKrakenTakerFeeAsync()
    {
        double? fee = await _cache.GetOrCreateAsync("KrakenTakerFee", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);

            try
            {
                var response = await _httpClient.GetStringAsync("https://api.kraken.com/0/public/AssetPairs?pair=USDCUSD");
                using var doc = JsonDocument.Parse(response);
                var root = doc.RootElement;

                if (root.TryGetProperty("error", out var errorArray) && errorArray.GetArrayLength() == 0)
                {
                    var fees = root.GetProperty("result").GetProperty("USDCUSD").GetProperty("fees");
                    if (fees.GetArrayLength() > 0)
                    {
                        double feePercent = fees[0][1].GetDouble();
                        return feePercent / 100.0;
                    }
                }
            }
            catch (Exception) { }

            return 0.0026;
        });

        return fee ?? 0.0026;
    }

    private async Task<double> FetchFiatRateAsync(string targetCurrency)
    {
        string cacheKey = $"FiatRate_{targetCurrency}";

        return await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);

            try
            {
                var response = await _httpClient.GetStringAsync($"https://v6.exchangerate-api.com/v6/{_apiSettings.ExchangeRateApiKey}/latest/SEK");
                using var doc = JsonDocument.Parse(response);

                if (doc.RootElement.GetProperty("conversion_rates").TryGetProperty(targetCurrency, out var rate))
                {
                    return rate.GetDouble();
                }
            }
            catch (Exception) { }

            var fallbacks = new Dictionary<string, double> { { "USD", 0.106 } };
            return fallbacks.GetValueOrDefault(targetCurrency, 1.0);
        });
    }

    private async Task<double> FetchWiseFeeSekAsync(double amountSek, string targetCurrency)
    {
        string cacheKey = $"WiseFee_{amountSek}_{targetCurrency}";

        return await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2);

            try
            {
                var url = $"https://api.transferwise.com/v3/quotes?sourceCurrency=SEK&targetCurrency={targetCurrency}&sourceAmount={amountSek}";
                var response = await _httpClient.GetStringAsync(url);
                using var doc = JsonDocument.Parse(response);

                var root = doc.RootElement;
                if (root.TryGetProperty("paymentOptions", out var options))
                {
                    foreach (var option in options.EnumerateArray())
                    {
                        if (option.GetProperty("disabled").GetBoolean() == false)
                        {
                            return option.GetProperty("fee").GetProperty("amount").GetDouble();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Wise API misslyckades. Använder fallback.");
            }

            return 5.0 + (amountSek * 0.005);
        });
    }

    private async Task<Dictionary<string, double>> FetchCryptoPricesInSekAsync()
    {
        return await _cache.GetOrCreateAsync("CryptoPricesSEK", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2);
            var prices = new Dictionary<string, double>();

            try
            {
                var requestUrl = "https://api.coingecko.com/api/v3/simple/price?ids=solana,polygon,ethereum,tron&vs_currencies=sek";
                using var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
                request.Headers.Add("x-cg-api-key", _apiSettings.CoinGeckoApiKey);

                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                var responseString = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(responseString);
                var root = doc.RootElement;

                if (root.TryGetProperty("solana", out var sol) && sol.TryGetProperty("sek", out var solSek))
                    prices["solana"] = solSek.GetDouble();

                if (root.TryGetProperty("polygon", out var matic) && matic.TryGetProperty("sek", out var maticSek))
                    prices["polygon"] = maticSek.GetDouble();

                if (root.TryGetProperty("ethereum", out var eth) && eth.TryGetProperty("sek", out var ethSek))
                    prices["ethereum"] = ethSek.GetDouble();

                if (root.TryGetProperty("tron", out var tron) && tron.TryGetProperty("sek", out var tronSek))
                    prices["tron"] = tronSek.GetDouble();
            }
            catch (Exception)
            {
                prices["solana"] = 1550.0;
                prices["polygon"] = 11.50;
                prices["ethereum"] = 36000.0;
                prices["tron"] = 1.20;
            }

            return prices;
        });
    }
}

public record Web3Network(string Name, double GasFeeSek, int TimeInSeconds, string Stablecoin);