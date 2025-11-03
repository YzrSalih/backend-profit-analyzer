using Microsoft.EntityFrameworkCore;

namespace CyclopsMini.Data
{
public class AppDbContext : DbContext
{
public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }


public DbSet<Product> Products => Set<Product>();
public DbSet<Sale> Sales => Set<Sale>();
public DbSet<Expense> Expenses => Set<Expense>();
}


public class Product
{
public int Id { get; set; }
public string Sku { get; set; } = string.Empty;
public string Title { get; set; } = string.Empty;
public decimal CostPrice { get; set; }
public DateTime CreatedAt { get; set; }
}


public class Sale
{
public int Id { get; set; }
public int ProductId { get; set; }
public int Quantity { get; set; }
public decimal UnitPrice { get; set; }
public decimal MarketplaceFee { get; set; }
public decimal ShippingCost { get; set; }
public DateTime SaleDate { get; set; }


public Product? Product { get; set; }
}


public class Expense
{
public int Id { get; set; }
public string Type { get; set; } = string.Empty;
public decimal Amount { get; set; }
public DateTime OccurredAt { get; set; }
}
}