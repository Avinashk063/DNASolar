using Microsoft.EntityFrameworkCore;
using DNASoftech.Domain.Interfaces;
using DNASoftech.Domain.Models.ECommerce;
using DNASoftech.Infrastructure.Data;

namespace DNASoftech.Infrastructure.Repository.ECommerce
{
    public class ReviewRepository : IReviewRepository
    {
        private readonly DNASoftechDB _dbContext;

        public ReviewRepository(DNASoftechDB dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<ReviewSummary?> GetSummaryByProductIdAsync(int productId, CancellationToken cancellationToken = default)
        {
            return await _dbContext.Reviews
                .Where(r => r.ProductId == productId)
                .GroupBy(r => r.ProductId)
                .Select(g => new ReviewSummary(g.Key, Math.Round(g.Average(x => (double)x.Rating), 1), g.Count()))
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<IReadOnlyCollection<ReviewSummary>> GetSummariesByProductIdsAsync(IEnumerable<int> productIds, CancellationToken cancellationToken = default)
        {
            var ids = productIds.Distinct().ToArray();
            if (ids.Length == 0)
            {
                return Array.Empty<ReviewSummary>();
            }

            return await _dbContext.Reviews
                .Where(r => ids.Contains(r.ProductId))
                .GroupBy(r => r.ProductId)
                .Select(g => new ReviewSummary(g.Key, Math.Round(g.Average(x => (double)x.Rating), 1), g.Count()))
                .ToListAsync(cancellationToken);
        }
    }
}
