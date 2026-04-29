using Microsoft.EntityFrameworkCore;
using DNASoftech.Domain.Interfaces;
using DNASoftech.Domain.Models.ECommerce;
using DNASoftech.Infrastructure.Data;

namespace DNASoftech.Infrastructure.Repository.ECommerce
{
    public class WishlistRepository : IWishlistRepository
    {
        private readonly DNASoftechDB _dbContext;

        public WishlistRepository(DNASoftechDB dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<List<WishlistItem>> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default)
        {
            return await _dbContext.WishlistItems
                .Include(w => w.Product)
                .Where(w => w.UserId == userId)
                .ToListAsync(cancellationToken);
        }

        public async Task<WishlistItem?> GetItemAsync(string userId, int productId, CancellationToken cancellationToken = default)
        {
            return await _dbContext.WishlistItems
                .FirstOrDefaultAsync(w => w.UserId == userId && w.ProductId == productId, cancellationToken);
        }

        public async Task AddAsync(WishlistItem item, CancellationToken cancellationToken = default)
        {
            await _dbContext.WishlistItems.AddAsync(item, cancellationToken);
        }

        public Task RemoveAsync(WishlistItem item, CancellationToken cancellationToken = default)
        {
            _dbContext.WishlistItems.Remove(item);
            return Task.CompletedTask;
        }

        public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
