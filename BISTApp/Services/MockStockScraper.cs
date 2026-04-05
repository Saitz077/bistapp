using BISTApp.Models;

namespace BISTApp.Services;

/// <summary>
/// Test ve geliştirme için mock veri üreten scraper
/// Gerçek web scraping yerine örnek veri döner
/// </summary>
public class MockStockScraper : IStockScraper
{
    private readonly Random _random = new();
    private readonly ILogger<MockStockScraper> _logger;

    public MockStockScraper(ILogger<MockStockScraper> logger)
    {
        _logger = logger;
    }

    public Task<List<Stock>> ScrapeStocksAsync(List<string> symbols)
    {
        _logger.LogInformation("Mock scraper generating data for {Count} symbols", symbols.Count);

        var stocks = symbols.Select(symbol => new Stock
        {
            Symbol = symbol.ToUpper(),
            Price = Math.Round((decimal)(_random.NextDouble() * 100 + 10), 2), // 10-110 arası rastgele fiyat
            Change = Math.Round((decimal)(_random.NextDouble() * 10 - 5), 2), // -5% ile +5% arası değişim
            UpdatedAt = DateTime.UtcNow
        }).ToList();

        return Task.FromResult(stocks);
    }
}
