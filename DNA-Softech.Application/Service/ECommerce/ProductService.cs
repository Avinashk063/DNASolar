using DNASoftech.Application.DTOs.Product;
using DNASoftech.Application.Interface;
using DNASoftech.Domain.Interfaces;

namespace DNASoftech.Application.Service.ECommerce
{
    public class ProductService : IProductService
    {
        private readonly IProductRepository _productRepository;
        private readonly IReviewRepository _reviewRepository;

        public ProductService(IProductRepository productRepository, IReviewRepository reviewRepository)
        {
            _productRepository = productRepository;
            _reviewRepository = reviewRepository;
        }

        public async Task<IReadOnlyCollection<ProductDto>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            var products = await _productRepository.GetAllAsync(cancellationToken);
            var summaries = await _reviewRepository.GetSummariesByProductIdsAsync(products.Select(p => p.ProductId), cancellationToken);
            var summaryMap = summaries.ToDictionary(s => s.ProductId, s => s);

            return products.Select(p => Map(p, summaryMap.TryGetValue(p.ProductId, out var summary) ? summary : null)).ToList();
        }

        public async Task<ProductDto?> GetByIdAsync(int productId, CancellationToken cancellationToken = default)
        {
            var product = await _productRepository.GetByIdAsync(productId, cancellationToken);
            if (product is null)
            {
                return null;
            }

            var summary = await _reviewRepository.GetSummaryByProductIdAsync(productId, cancellationToken);
            return Map(product, summary);
        }

        private static ProductDto Map(DNASoftech.Domain.Models.ECommerce.Product product, DNASoftech.Domain.Models.ECommerce.ReviewSummary? summary)
        {
            return new ProductDto
            {
                Id = product.ProductId,
                Name = product.Name,
                Description = product.Description,
                Category = (product.Category ?? string.Empty).ToLowerInvariant(),
                Price = product.Price,
                OriginalPrice = product.OriginalPrice,
                ImageUrl = product.ImageUrl,
                InStock = product.InStock,
                Discount = product.Discount,
                Rating = summary?.AverageRating ?? product.Rating,
                Reviews = summary?.ReviewCount ?? product.Reviews
            };
        }
    }
}
