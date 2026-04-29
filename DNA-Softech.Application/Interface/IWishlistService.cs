using DNASoftech.Application.DTOs.Wishlist;

namespace DNASoftech.Application.Interface
{
    public interface IWishlistService
    {
        Task<WishlistDto> GetWishlistAsync(string userId, CancellationToken cancellationToken = default);
        Task<WishlistDto> AddItemAsync(AddWishlistItemRequestDto request, CancellationToken cancellationToken = default);
        Task<WishlistDto> RemoveItemAsync(string userId, int productId, CancellationToken cancellationToken = default);
    }
}
