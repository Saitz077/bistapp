using HtmlAgilityPack;
using BISTApp.Models;
using System.Text.RegularExpressions;
using System.Text;

namespace BISTApp.Services;

public class YahooStockScraper : IStockScraper
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<YahooStockScraper> _logger;

    public YahooStockScraper(HttpClient httpClient, ILogger<YahooStockScraper> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
    }

    public async Task<List<Stock>> ScrapeStocksAsync(List<string> symbols)
    {
        var stocks = new List<Stock>();

        foreach (var symbol in symbols)
        {
            try
            {
                var stock = await ScrapeSingleStockAsync(symbol);
                if (stock != null)
                {
                    stocks.Add(stock);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to scrape stock {Symbol}", symbol);
            }
        }

        return stocks;
    }

    private async Task<Stock?> ScrapeSingleStockAsync(string symbol)
    {
        try
        {
            // Yahoo Finance için BIST sembol formatı (örnek: AKBNK.IS)
            var yahooSymbol = $"{symbol.ToUpper()}.IS";
            var url = $"https://finance.yahoo.com/quote/{yahooSymbol}";

            var response = await _httpClient.GetStringAsync(url);
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(response);

            // Yahoo Finance fiyat bilgisi
            var priceNode = htmlDoc.DocumentNode.SelectSingleNode("//fin-streamer[@data-field='regularMarketPrice']")
                         ?? htmlDoc.DocumentNode.SelectSingleNode("//span[contains(@class, 'Trsdu(0.3s)')]");

            if (priceNode == null)
            {
                _logger.LogWarning("Price not found for {Symbol}", symbol);
                return null;
            }

            var priceText = priceNode.InnerText.Trim();
            var price = ParsePrice(priceText);

            // Değişim yüzdesi
            var changeNode = htmlDoc.DocumentNode.SelectSingleNode("//fin-streamer[@data-field='regularMarketChangePercent']")
                         ?? htmlDoc.DocumentNode.SelectSingleNode("//span[contains(@data-reactid, 'changePercent')]");

            decimal change = 0;
            if (changeNode != null)
            {
                var changeText = changeNode.InnerText.Trim();
                change = ParseChange(changeText);
            }

            return new Stock
            {
                Symbol = symbol.ToUpper(),
                Price = price,
                Change = change,
                UpdatedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scraping {Symbol}", symbol);
            return null;
        }
    }

    private decimal ParsePrice(string priceText)
    {
        var cleanText = Regex.Replace(priceText, @"[^\d.,]", "");
        cleanText = cleanText.Replace(",", ".");
        
        if (decimal.TryParse(cleanText, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var price))
        {
            return price;
        }

        return 0;
    }

    private decimal ParseChange(string changeText)
    {
        var cleanText = Regex.Replace(changeText, @"[^\d.,+-]", "");
        cleanText = cleanText.Replace(",", ".");
        
        if (decimal.TryParse(cleanText, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var change))
        {
            return change;
        }

        return 0;
    }
}
