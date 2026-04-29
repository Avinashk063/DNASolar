using DNASoftech.Application.DTOs.Cart;

namespace DNASoftech.Application.Interface
{
    public interface ICartService
    {
        Task<CartDto> GetCartAsync(string userId, CancellationToken cancellationToken = default);
        Task<CartDto> AddOrUpdateItemAsync(UpsertCartItemRequestDto request, CancellationToken cancellationToken = default);
        Task<CartDto> RemoveItemAsync(string userId, int productId, CancellationToken cancellationToken = default);
    }
}

