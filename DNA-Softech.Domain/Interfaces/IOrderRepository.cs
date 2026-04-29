using DNASoftech.Domain.Models.ECommerce;

namespace DNASoftech.Domain.Interfaces
{
    public interface IOrderRepository
    {
        Task AddAsync(Order order, CancellationToken cancellationToken = default);
        Task SaveChangesAsync(CancellationToken cancellationToken = default);
        Task<Order?> GetByIdAsync(int orderId, CancellationToken cancellationToken = default);
    }
}

