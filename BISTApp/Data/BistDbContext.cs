using Microsoft.EntityFrameworkCore;
using BISTApp.Models;

namespace BISTApp.Data;

public class BistDbContext : DbContext
{
    public BistDbContext(DbContextOptions<BistDbContext> options) : base(options)
    {
    }

    public DbSet<Stock> Stocks { get; set; }
    public DbSet<StockHistory> StockHistories { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Stock>()
            .HasIndex(s => s.Symbol)
            .IsUnique();

        modelBuilder.Entity<StockHistory>()
            .HasIndex(sh => new { sh.Symbol, sh.Date })
            .IsUnique();
    }
}
