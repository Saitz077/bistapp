namespace BISTApp.Models;

public class Stock
{
    public int Id { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal Change { get; set; }
    public DateTime UpdatedAt { get; set; }
}
