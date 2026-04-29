using Microsoft.EntityFrameworkCore;
using DNASoftech.Domain.Interfaces;
using DNASoftech.Domain.Models.ECommerce;
using DNASoftech.Infrastructure.Data;

namespace DNASoftech.Infrastructure.Repository.ECommerce
{
    public class ProductRepository : IProductRepository
    {
        private readonly DNASoftechDB _dbContext;

        public ProductRepository(DNASoftechDB dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<IReadOnlyCollection<Product>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return await _dbContext.Products.ToListAsync(cancellationToken);
        }

        public async Task<Product?> GetByIdAsync(int productId, CancellationToken cancellationToken = default)
        {
            return await _dbContext.Products.FirstOrDefaultAsync(p => p.ProductId == productId, cancellationToken);
        }

        public async Task<IReadOnlyCollection<Product>> GetByIdsAsync(IEnumerable<int> productIds, CancellationToken cancellationToken = default)
        {
            var ids = productIds.Distinct().ToArray();
            return await _dbContext.Products
                .Where(p => ids.Contains(p.ProductId))
                .ToListAsync(cancellationToken);
        }

        public async Task<List<Category>> GetCategoriesAsync(CancellationToken cancellationToken = default)
        {
            return await _dbContext.Categories.OrderBy(c => c.Name).ToListAsync(cancellationToken);
        }
    }
}

