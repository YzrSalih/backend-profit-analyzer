using System.Globalization;


namespace CyclopsMini.Data
{
public static class DbSeeder
{
public static void Seed(AppDbContext db)
{
if (db.Products.Any()) return;


var p1 = new Product { Sku = "SKU-001", Title = "Wireless Mouse", CostPrice = 8.50m, CreatedAt = DateTime.UtcNow };
var p2 = new Product { Sku = "SKU-002", Title = "USB-C Cable", CostPrice = 2.10m, CreatedAt = DateTime.UtcNow };
db.Products.AddRange(p1, p2);
db.SaveChanges();


var baseDate = DateTime.UtcNow.Date.AddDays(-14);
var rnd = new Random(7);


var sales = new List<Sale>();
for (int i = 0; i < 40; i++)
{
var prod = i % 2 == 0 ? p1 : p2;
sales.Add(new Sale
{
ProductId = prod.Id,
Quantity = rnd.Next(1, 4),
UnitPrice = prod == p1 ? 16.99m : 6.99m,
MarketplaceFee = prod == p1 ? 1.20m : 0.70m,
ShippingCost = prod == p1 ? 1.80m : 0.90m,
SaleDate = baseDate.AddDays(rnd.Next(0, 14)).AddHours(rnd.Next(0, 23))
});
}


db.Sales.AddRange(sales);
db.SaveChanges();
}
}
}