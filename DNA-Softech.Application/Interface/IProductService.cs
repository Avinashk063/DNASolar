using DNASoftech.Application.DTOs.Product;

namespace DNASoftech.Application.Interface
{
    public interface IProductService
    {
        Task<IReadOnlyCollection<ProductDto>> GetAllAsync(CancellationToken cancellationToken = default);
        Task<ProductDto?> GetByIdAsync(int productId, CancellationToken cancellationToken = default);
    }
}
