using CyclopsMini.Data;
using Microsoft.EntityFrameworkCore;

namespace CyclopsMini.Services
{
    public interface IMetricsService
    {
        Task<IEnumerable<PeriodProfit>> GetProfitAsync(AppDbContext db, DateTime from, DateTime to, string granularity, CancellationToken ct = default);
        Task<IEnumerable<TopProduct>> GetTopProductsAsync(AppDbContext db, int limit, CancellationToken ct = default);
    }

    public record PeriodProfit(string Period, decimal Profit);
    public record TopProduct(int Id, string Title, decimal Profit);

    public class MetricsService : IMetricsService
    {
        public async Task<IEnumerable<PeriodProfit>> GetProfitAsync(AppDbContext db, DateTime from, DateTime to, string granularity, CancellationToken ct = default)
        {
            var baseQuery = db.Sales
                .Where(s => s.SaleDate >= from && s.SaleDate <= to)
                .Join(db.Products, s => s.ProductId, p => p.Id, (s, p) => new { s, p })
                .Select(x => new
                {
                    x.s.SaleDate,
                    GrossProfit = (x.s.UnitPrice * x.s.Quantity)
                                  - (x.p.CostPrice * x.s.Quantity)
                                  - x.s.MarketplaceFee
                                  - x.s.ShippingCost
                });

            IQueryable<PeriodProfit> grouped = granularity.ToLower() switch
            {
                "daily" => baseQuery
                    .GroupBy(x => x.SaleDate.Date)
                    .Select(g => new PeriodProfit(g.Key.ToString("yyyy-MM-dd"), g.Sum(i => i.GrossProfit))),
                "weekly" => baseQuery
                    .GroupBy(x => System.Globalization.ISOWeek.GetYear(x.SaleDate) * 100 + System.Globalization.ISOWeek.GetWeekOfYear(x.SaleDate))
                    .Select(g => new PeriodProfit(g.Key.ToString(), g.Sum(i => i.GrossProfit))),
                _ => baseQuery
                    .GroupBy(x => x.SaleDate.Date)
                    .Select(g => new PeriodProfit(g.Key.ToString("yyyy-MM-dd"), g.Sum(i => i.GrossProfit)))
            };

            return await grouped.OrderBy(x => x.Period).ToListAsync(ct);
        }

        public async Task<IEnumerable<TopProduct>> GetTopProductsAsync(AppDbContext db, int limit, CancellationToken ct = default)
        {
            var take = limit <= 0 ? 5 : limit;

            var q = db.Sales
                .Join(db.Products, s => s.ProductId, p => p.Id, (s, p) => new { s, p })
                .GroupBy(x => new { x.p.Id, x.p.Title })
                .Select(g => new TopProduct(
                    g.Key.Id,
                    g.Key.Title,
                    g.Sum(i => (i.s.UnitPrice * i.s.Quantity) - (i.p.CostPrice * i.s.Quantity) - i.s.MarketplaceFee - i.s.ShippingCost)
                ))
                .OrderByDescending(x => x.Profit)
                .Take(take);

            return await q.ToListAsync(ct);
        }
    }
}
