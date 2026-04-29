using Microsoft.EntityFrameworkCore;
using DNASoftech.Domain.Interfaces;
using DNASoftech.Domain.Models.ECommerce;
using DNASoftech.Infrastructure.Data;

namespace DNASoftech.Infrastructure.Repository.ECommerce
{
    public class CartRepository : ICartRepository
    {
        private readonly DNASoftechDB _dbContext;

        public CartRepository(DNASoftechDB dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<Cart?> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default)
        {
            return await _dbContext.Carts
                .Include(c => c.Items)
                .ThenInclude(i => i.Product)
                .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);
        }

        public async Task AddAsync(Cart cart, CancellationToken cancellationToken = default)
        {
            await _dbContext.Carts.AddAsync(cart, cancellationToken);
        }

        public Task RemoveItemAsync(CartItem item, CancellationToken cancellationToken = default)
        {
            _dbContext.CartItems.Remove(item);
            return Task.CompletedTask;
        }

        public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}

