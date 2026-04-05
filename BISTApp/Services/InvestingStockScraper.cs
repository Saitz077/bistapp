using HtmlAgilityPack;
using BISTApp.Models;
using System.Text.RegularExpressions;

namespace BISTApp.Services;

public class InvestingStockScraper : IStockScraper
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<InvestingStockScraper> _logger;

    public InvestingStockScraper(HttpClient httpClient, ILogger<InvestingStockScraper> logger)
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
            // Investing.com BIST URL formatı (örnek: AKBNK için)
            var url = $"https://www.investing.com/equities/{symbol.ToLower()}-turkey";
            
            var response = await _httpClient.GetStringAsync(url);
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(response);

            // Investing.com'dan fiyat bilgisini çekme
            // Bu selector'lar site yapısına göre değişebilir
            var priceNode = htmlDoc.DocumentNode.SelectSingleNode("//span[@data-test='instrument-price-last']") 
                         ?? htmlDoc.DocumentNode.SelectSingleNode("//div[contains(@class, 'last-price-value')]");

            if (priceNode == null)
            {
                _logger.LogWarning("Price not found for {Symbol}", symbol);
                return null;
            }

            var priceText = priceNode.InnerText.Trim();
            var price = ParsePrice(priceText);

            // Değişim yüzdesi
            var changeNode = htmlDoc.DocumentNode.SelectSingleNode("//span[contains(@class, 'instrument-price-change')]")
                         ?? htmlDoc.DocumentNode.SelectSingleNode("//span[@data-test='instrument-price-change-percent']");
            
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
        // Fiyat string'ini temizle (örnek: "45.50" veya "45,50 TL" -> 45.50)
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
        // Değişim yüzdesini parse et (örnek: "+2.5%" veya "-1.2%")
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
