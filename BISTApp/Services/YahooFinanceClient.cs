using System.Text.Json;

namespace BISTApp.Services;

/// <summary>
/// Yahoo Finance üzerinden gecikmeli günlük kapanış verisini çeker.
/// HTML scraping yerine Yahoo'nun chart JSON endpoint'i kullanılır (daha stabil).
/// </summary>
public class YahooFinanceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<YahooFinanceClient> _logger;

    public YahooFinanceClient(HttpClient httpClient, ILogger<YahooFinanceClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
        {
            _httpClient.DefaultRequestHeaders.Add(
                "User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/121 Safari/537.36");
        }
    }

    /// <summary>
    /// Son N gün için günlük kapanışları getirir.
    /// </summary>
    public async Task<List<YahooDailyClose>> GetDailyClosesAsync(string bistSymbol, int days = 30, CancellationToken ct = default)
    {
        // Yahoo BIST sembol formatı: AKBNK.IS
        var yahooSymbol = $"{bistSymbol.ToUpperInvariant()}.IS";

        // 1d interval, son N gün aralığı
        var period2 = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var period1 = DateTimeOffset.UtcNow.AddDays(-(days + 5)).ToUnixTimeSeconds(); // bir miktar buffer

        var url =
            $"https://query2.finance.yahoo.com/v8/finance/chart/{Uri.EscapeDataString(yahooSymbol)}" +
            $"?interval=1d&period1={period1}&period2={period2}&includePrePost=false&events=div%7Csplit";

        try
        {
            using var resp = await _httpClient.GetAsync(url, ct);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("Yahoo chart request failed for {Symbol}: {StatusCode}", yahooSymbol, (int)resp.StatusCode);
                return new List<YahooDailyClose>();
            }

            var json = await resp.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);

            // Defensive parse:
            // chart.result[0].timestamp[] (unix seconds)
            // chart.result[0].indicators.quote[0].close[]
            var chart = doc.RootElement.GetProperty("chart");
            var error = chart.GetProperty("error");
            if (error.ValueKind != JsonValueKind.Null)
            {
                _logger.LogWarning("Yahoo chart error for {Symbol}: {Error}", yahooSymbol, error.ToString());
                return new List<YahooDailyClose>();
            }

            var result = chart.GetProperty("result");
            if (result.ValueKind != JsonValueKind.Array || result.GetArrayLength() == 0)
                return new List<YahooDailyClose>();

            var r0 = result[0];
            if (!r0.TryGetProperty("timestamp", out var timestamps) || timestamps.ValueKind != JsonValueKind.Array)
                return new List<YahooDailyClose>();

            if (!r0.TryGetProperty("indicators", out var indicators))
                return new List<YahooDailyClose>();

            if (!indicators.TryGetProperty("quote", out var quoteArr) || quoteArr.ValueKind != JsonValueKind.Array || quoteArr.GetArrayLength() == 0)
                return new List<YahooDailyClose>();

            var quote0 = quoteArr[0];
            if (!quote0.TryGetProperty("close", out var closeArr) || closeArr.ValueKind != JsonValueKind.Array)
                return new List<YahooDailyClose>();

            var closes = new List<YahooDailyClose>();
            var len = Math.Min(timestamps.GetArrayLength(), closeArr.GetArrayLength());

            for (var i = 0; i < len; i++)
            {
                // close null olabiliyor (tatil/eksik data)
                if (closeArr[i].ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) continue;

                if (!closeArr[i].TryGetDecimal(out var close)) continue;

                var ts = timestamps[i].GetInt64();
                var date = DateTimeOffset.FromUnixTimeSeconds(ts).UtcDateTime.Date;

                closes.Add(new YahooDailyClose(date, close));
            }

            // Son N trading day
            return closes
                .OrderBy(x => x.Date)
                .TakeLast(days)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Yahoo chart parse failed for {Symbol}", yahooSymbol);
            return new List<YahooDailyClose>();
        }
    }
}

public record YahooDailyClose(DateTime Date, decimal Close);

