using DNASoftech.Domain.Models.ECommerce;

namespace DNASoftech.Domain.Interfaces
{
    public interface IWishlistRepository
    {
        Task<List<WishlistItem>> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default);
        Task<WishlistItem?> GetItemAsync(string userId, int productId, CancellationToken cancellationToken = default);
        Task AddAsync(WishlistItem item, CancellationToken cancellationToken = default);
        Task RemoveAsync(WishlistItem item, CancellationToken cancellationToken = default);
        Task SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
