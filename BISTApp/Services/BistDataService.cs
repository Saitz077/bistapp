using BISTApp.Data;
using BISTApp.Models;
using Microsoft.EntityFrameworkCore;

namespace BISTApp.Services;

public class BistDataService
{
    private readonly BistDbContext _context;
    private readonly FileSymbolService _symbolService;
    private readonly YahooFinanceClient _yahoo;
    private readonly ILogger<BistDataService> _logger;

    public BistDataService(
        BistDbContext context,
        FileSymbolService symbolService,
        YahooFinanceClient yahoo,
        ILogger<BistDataService> logger)
    {
        _context = context;
        _symbolService = symbolService;
        _yahoo = yahoo;
        _logger = logger;
    }

    /// <summary>
    /// Dosyadan tüm BIST sembollerini çekip DB'ye upsert eder.
    /// </summary>
    public async Task UpdateSymbolsAsync(CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Starting symbol list update from file...");

            var symbols = await _symbolService.GetAllBistSymbolsAsync(ct);
            if (symbols.Count == 0)
            {
                _logger.LogWarning("Sembol listesi alınamadı; önceki semboller kullanılacak.");
                return;
            }

            var existing = await _context.Stocks.Select(s => s.Symbol).ToListAsync(ct);
            var existingSet = existing.ToHashSet(StringComparer.OrdinalIgnoreCase);

            var now = DateTime.UtcNow;
            var added = 0;

            foreach (var sym in symbols)
            {
                if (existingSet.Contains(sym)) continue;

                _context.Stocks.Add(new Stock
                {
                    Symbol = sym,
                    Price = 0,
                    Change = 0,
                    UpdatedAt = now
                });
                added++;
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("Symbol list updated. Total: {Total}, Added: {Added}", symbols.Count, added);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating symbol list");
        }
    }

    /// <summary>
    /// DB'deki tüm semboller için Yahoo'dan günlük kapanışları çekip history ve güncel fiyatı günceller.
    /// </summary>
    public async Task UpdatePricesAndHistoryAsync(int days = 30, CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Starting prices/history update from Yahoo. Days={Days}", days);

            var symbols = await _context.Stocks
                .Select(s => s.Symbol)
                .OrderBy(s => s)
                .ToListAsync(ct);

            var now = DateTime.UtcNow;
            int counter = 0;

            foreach (var symbol in symbols)
            {
                ct.ThrowIfCancellationRequested();
                
                // Yahoo'yu boğmamak için her istek arasında kısa bekleme (Rate Limit koruması)
                await Task.Delay(250, ct);

                try
                {
                    var closes = await _yahoo.GetDailyClosesAsync(symbol, days, ct);
                    if (closes.Count < 2)
                        continue;

                    // Upsert history (tarih+sembol unique)
                    foreach (var c in closes)
                    {
                        var exists = await _context.StockHistories
                            .AnyAsync(h => h.Symbol == symbol && h.Date == c.Date, ct);

                        if (!exists)
                        {
                            _context.StockHistories.Add(new StockHistory
                            {
                                Symbol = symbol,
                                Date = c.Date,
                                Price = c.Close
                            });
                        }
                    }

                    // Güncel Stock güncelle
                    var last = closes[^1];
                    var prev = closes[^2];
                    var changePct = prev.Close == 0 ? 0 : ((last.Close - prev.Close) / prev.Close) * 100m;

                    var stock = await _context.Stocks.FirstOrDefaultAsync(s => s.Symbol == symbol, ct);
                    if (stock != null)
                    {
                        stock.Price = last.Close;
                        stock.Change = Math.Round(changePct, 2);
                        stock.UpdatedAt = now;
                    }

                    // Her 5 hissede bir kaydet ki kullanıcı ekranda akışı görsün
                    counter++;
                    if (counter % 5 == 0)
                    {
                        await _context.SaveChangesAsync(ct);
                    }
                }
                catch (Exception exOne)
                {
                    _logger.LogWarning(exOne, "Yahoo update failed for {Symbol}", symbol);
                }
            }
            
            // Döngü bitince son kalanları kaydet
            await _context.SaveChangesAsync(ct);

            _logger.LogInformation("Prices/history update completed.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating prices/history");
        }
    }

    public async Task<List<Stock>> GetAllStocksAsync()
    {
        return await _context.Stocks
            .OrderBy(s => s.Symbol)
            .ToListAsync();
    }

    public async Task<Stock?> GetStockBySymbolAsync(string symbol)
    {
        return await _context.Stocks
            .FirstOrDefaultAsync(s => s.Symbol == symbol.ToUpper());
    }

    public async Task<List<StockHistory>> GetStockHistoryAsync(string symbol, int days = 30)
    {
        var startDate = DateTime.UtcNow.Date.AddDays(-days);
        
        return await _context.StockHistories
            .Where(h => h.Symbol == symbol.ToUpper() && h.Date >= startDate)
            .OrderBy(h => h.Date)
            .ToListAsync();
    }

    /// <summary>
    /// Tüm hisselerin son N günlük history'sini getirir (performans için)
    /// </summary>
    public async Task<Dictionary<string, List<StockHistory>>> GetAllStocksHistoryAsync(int days = 10)
    {
        var startDate = DateTime.UtcNow.Date.AddDays(-days);
        
        var histories = await _context.StockHistories
            .Where(h => h.Date >= startDate)
            .OrderBy(h => h.Symbol)
            .ThenBy(h => h.Date)
            .ToListAsync();

            return histories
            .GroupBy(h => h.Symbol)
            .ToDictionary(g => g.Key, g => g.ToList());
    }
}
