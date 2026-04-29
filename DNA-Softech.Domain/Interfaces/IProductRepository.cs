using DNASoftech.Domain.Models.ECommerce;

namespace DNASoftech.Domain.Interfaces
{
    public interface IProductRepository
    {
        Task<IReadOnlyCollection<Product>> GetAllAsync(CancellationToken cancellationToken = default);
        Task<Product?> GetByIdAsync(int productId, CancellationToken cancellationToken = default);
        Task<IReadOnlyCollection<Product>> GetByIdsAsync(IEnumerable<int> productIds, CancellationToken cancellationToken = default);
        Task<List<Category>> GetCategoriesAsync(CancellationToken cancellationToken = default);
    }
}

