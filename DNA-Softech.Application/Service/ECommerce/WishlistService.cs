using DNASoftech.Application.DTOs.Wishlist;
using DNASoftech.Application.Interface;
using DNASoftech.Domain.Interfaces;
using DNASoftech.Domain.Models.ECommerce;

namespace DNASoftech.Application.Service.ECommerce
{
    public class WishlistService : IWishlistService
    {
        private readonly IWishlistRepository _wishlistRepository;
        private readonly IProductRepository _productRepository;

        public WishlistService(IWishlistRepository wishlistRepository, IProductRepository productRepository)
        {
            _wishlistRepository = wishlistRepository;
            _productRepository = productRepository;
        }

        public async Task<WishlistDto> GetWishlistAsync(string userId, CancellationToken cancellationToken = default)
        {
            var items = await _wishlistRepository.GetByUserIdAsync(userId, cancellationToken);
            return new WishlistDto
            {
                UserId = userId,
                Items = items.Select(i => new WishlistItemDto
                {
                    ProductId = i.ProductId,
                    ProductName = i.Product?.Name ?? string.Empty,
                    UnitPrice = i.Product?.Price ?? 0m
                }).ToList()
            };
        }

        public async Task<WishlistDto> AddItemAsync(AddWishlistItemRequestDto request, CancellationToken cancellationToken = default)
        {
            var product = await _productRepository.GetByIdAsync(request.ProductId, cancellationToken);
            if (product == null)
            {
                throw new InvalidOperationException($"Product {request.ProductId} was not found.");
            }

            var existingItem = await _wishlistRepository.GetItemAsync(request.UserId, request.ProductId, cancellationToken);
            if (existingItem == null)
            {
                var item = new WishlistItem
                {
                    UserId = request.UserId,
                    ProductId = request.ProductId
                };
                await _wishlistRepository.AddAsync(item, cancellationToken);
                await _wishlistRepository.SaveChangesAsync(cancellationToken);
            }

            return await GetWishlistAsync(request.UserId, cancellationToken);
        }

        public async Task<WishlistDto> RemoveItemAsync(string userId, int productId, CancellationToken cancellationToken = default)
        {
            var existingItem = await _wishlistRepository.GetItemAsync(userId, productId, cancellationToken);
            if (existingItem != null)
            {
                await _wishlistRepository.RemoveAsync(existingItem, cancellationToken);
                await _wishlistRepository.SaveChangesAsync(cancellationToken);
            }

            return await GetWishlistAsync(userId, cancellationToken);
        }
    }
}
