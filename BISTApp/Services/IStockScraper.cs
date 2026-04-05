using BISTApp.Models;

namespace BISTApp.Services;

public interface IStockScraper
{
    Task<List<Stock>> ScrapeStocksAsync(List<string> symbols);
}
