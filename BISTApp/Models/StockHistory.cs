namespace BISTApp.Models;

public class StockHistory
{
    public int Id { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public DateTime Date { get; set; }
}
