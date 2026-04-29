using DNASoftech.Application.DTOs.Cart;
using DNASoftech.Application.Interface;
using DNASoftech.Domain.Interfaces;
using DNASoftech.Domain.Models.ECommerce;

namespace DNASoftech.Application.Service.ECommerce
{
    public class CartService : ICartService
    {
        private readonly ICartRepository _cartRepository;
        private readonly IProductRepository _productRepository;

        public CartService(ICartRepository cartRepository, IProductRepository productRepository)
        {
            _cartRepository = cartRepository;
            _productRepository = productRepository;
        }

        public async Task<CartDto> GetCartAsync(string userId, CancellationToken cancellationToken = default)
        {
            var cart = await _cartRepository.GetByUserIdAsync(userId, cancellationToken);
            return cart is null ? new CartDto { UserId = userId } : Map(cart);
        }

        public async Task<CartDto> AddOrUpdateItemAsync(UpsertCartItemRequestDto request, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(request.UserId))
            {
                throw new ArgumentException("UserId is required.");
            }

            if (request.ProductId <= 0)
            {
                throw new ArgumentException("ProductId must be greater than zero.");
            }

            var product = await _productRepository.GetByIdAsync(request.ProductId, cancellationToken);
            if (product is null)
            {
                throw new InvalidOperationException($"Product {request.ProductId} was not found.");
            }

            var cart = await _cartRepository.GetByUserIdAsync(request.UserId, cancellationToken);
            if (cart is null)
            {
                cart = new Cart { UserId = request.UserId };
                await _cartRepository.AddAsync(cart, cancellationToken);
            }

            var existingItem = cart.Items.FirstOrDefault(i => i.ProductId == request.ProductId);
            if (request.Quantity <= 0)
            {
                if (existingItem is not null)
                {
                    cart.RemoveItem(request.ProductId);
                    await _cartRepository.RemoveItemAsync(existingItem, cancellationToken);
                }
            }
            else
            {
                cart.AddItem(request.ProductId, request.Quantity);
            }
            await _cartRepository.SaveChangesAsync(cancellationToken);
            return Map(cart);
        }

        public async Task<CartDto> RemoveItemAsync(string userId, int productId, CancellationToken cancellationToken = default)
        {
            var cart = await _cartRepository.GetByUserIdAsync(userId, cancellationToken);
            if (cart is null)
            {
                return new CartDto { UserId = userId };
            }

            var existingItem = cart.Items.FirstOrDefault(i => i.ProductId == productId);
            if (existingItem is not null)
            {
                cart.RemoveItem(productId);
                await _cartRepository.RemoveItemAsync(existingItem, cancellationToken);
                await _cartRepository.SaveChangesAsync(cancellationToken);
            }

            return Map(cart);
        }

        private static CartDto Map(Cart cart)
        {
            return new CartDto
            {
                UserId = cart.UserId,
                Items = cart.Items.Select(i => new CartItemDto
                {
                    ProductId = i.ProductId,
                    ProductName = i.Product?.Name ?? string.Empty,
                    UnitPrice = i.Product?.Price ?? 0,
                    Quantity = i.Quantity
                }).ToList()
            };
        }
    }
}

