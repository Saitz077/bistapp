using Microsoft.AspNetCore.Mvc;
using BISTApp.Models;
using BISTApp.Services;

namespace BISTApp.Controllers;

[ApiController]
[Route("api/bist")]
public class BistController : ControllerBase
{
    private readonly BistDataService _dataService;
    private readonly ILogger<BistController> _logger;

    public BistController(BistDataService dataService, ILogger<BistController> logger)
    {
        _dataService = dataService;
        _logger = logger;
    }

    /// <summary>
    /// Tüm BIST hisselerini getirir
    /// </summary>
    [HttpGet("stocks")]
    [ProducesResponseType(typeof(List<Stock>), 200)]
    public async Task<ActionResult<List<Stock>>> GetAllStocks()
    {
        try
        {
            var stocks = await _dataService.GetAllStocksAsync();
            return Ok(stocks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all stocks");
            return StatusCode(500, "An error occurred while retrieving stocks");
        }
    }

    /// <summary>
    /// Belirli bir hissenin bilgilerini getirir
    /// </summary>
    [HttpGet("stocks/{symbol}")]
    [ProducesResponseType(typeof(Stock), 200)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<Stock>> GetStock(string symbol)
    {
        try
        {
            var stock = await _dataService.GetStockBySymbolAsync(symbol);
            
            if (stock == null)
            {
                return NotFound($"Stock with symbol '{symbol}' not found");
            }

            return Ok(stock);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting stock {Symbol}", symbol);
            return StatusCode(500, "An error occurred while retrieving stock");
        }
    }

    /// <summary>
    /// Belirli bir hissenin geçmiş fiyatlarını getirir (varsayılan: son 30 gün)
    /// </summary>
    [HttpGet("history/{symbol}")]
    [ProducesResponseType(typeof(List<StockHistory>), 200)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<List<StockHistory>>> GetStockHistory(string symbol, [FromQuery] int days = 30)
    {
        try
        {
            // Günlük limit kontrolü
            if (days > 365)
            {
                return BadRequest("Maximum 365 days allowed");
            }

            var stock = await _dataService.GetStockBySymbolAsync(symbol);
            if (stock == null)
            {
                return NotFound($"Stock with symbol '{symbol}' not found");
            }

            var history = await _dataService.GetStockHistoryAsync(symbol, days);
            return Ok(history);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting stock history for {Symbol}", symbol);
            return StatusCode(500, "An error occurred while retrieving stock history");
        }
    }

    /// <summary>
    /// Tüm hisselerin geçmiş fiyatlarını toplu olarak getirir (frontend için optimize)
    /// </summary>
    [HttpGet("history/all")]
    [ProducesResponseType(typeof(Dictionary<string, List<StockHistory>>), 200)]
    public async Task<ActionResult<Dictionary<string, List<StockHistory>>>> GetAllStocksHistory([FromQuery] int days = 10)
    {
        try
        {
            if (days > 365)
            {
                return BadRequest("Maximum 365 days allowed");
            }

            var history = await _dataService.GetAllStocksHistoryAsync(days);
            return Ok(history);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all stocks history");
            return StatusCode(500, "An error occurred while retrieving stocks history");
        }
    }

    /// <summary>
    /// Veri güncellemesini manuel olarak tetikler (test için)
    /// </summary>
    [HttpPost("update")]
    public async Task<ActionResult> TriggerUpdate()
    {
        try
        {
            // 1) semboller (KAP)
            await _dataService.UpdateSymbolsAsync(HttpContext.RequestAborted);
            // 2) prices/history (Yahoo)
            await _dataService.UpdatePricesAndHistoryAsync(days: 60, ct: HttpContext.RequestAborted);

            return Ok(new { message = "Sembol + fiyat/history güncellemesi tamamlandı" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error triggering update");
            return StatusCode(500, "Veri güncellemesi sırasında hata oluştu");
        }
    }
}
