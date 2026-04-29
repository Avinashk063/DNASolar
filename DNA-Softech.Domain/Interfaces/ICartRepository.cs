using DNASoftech.Domain.Models.ECommerce;

namespace DNASoftech.Domain.Interfaces
{
    public interface ICartRepository
    {
        Task<Cart?> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default);
        Task AddAsync(Cart cart, CancellationToken cancellationToken = default);
        Task RemoveItemAsync(CartItem item, CancellationToken cancellationToken = default);
        Task SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}

