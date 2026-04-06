using BISTApp.Data;
using Microsoft.EntityFrameworkCore;

namespace BISTApp.Services;

public class StockUpdateBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<StockUpdateBackgroundService> _logger;
    private readonly TimeSpan _updateInterval = TimeSpan.FromMinutes(7); // 7 dakika
    private readonly TimeSpan _symbolRefreshInterval = TimeSpan.FromHours(12);
    private DateTime _lastSymbolRefreshUtc = DateTime.MinValue;

    public StockUpdateBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<StockUpdateBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Stock Update Background Service started");

        // İlk çalıştırmada hemen sembolleri + veriyi çek
        await UpdateAllAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_updateInterval, stoppingToken);
                await UpdateAllAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in background service");
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); // Hata durumunda 1 dk bekle
            }
        }

        _logger.LogInformation("Stock Update Background Service stopped");
    }

    private async Task UpdateAllAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var dataService = scope.ServiceProvider.GetRequiredService<BistDataService>();

        // Sembol listesini daha seyrek yenile
        if (DateTime.UtcNow - _lastSymbolRefreshUtc > _symbolRefreshInterval)
        {
            await dataService.UpdateSymbolsAsync(ct);
            _lastSymbolRefreshUtc = DateTime.UtcNow;
        }

        // Fiyat + history daha sık; sadece son 20 günü tutacağız
        await dataService.UpdatePricesAndHistoryAsync(days: 20, ct: ct);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stock Update Background Service is stopping");
        await base.StopAsync(cancellationToken);
    }
}
