using Microsoft.EntityFrameworkCore;
using DNASoftech.Domain.Interfaces;
using DNASoftech.Domain.Models.ECommerce;
using DNASoftech.Infrastructure.Data;

namespace DNASoftech.Infrastructure.Repository.ECommerce
{
    public class OrderRepository : IOrderRepository
    {
        private readonly DNASoftechDB _dbContext;

        public OrderRepository(DNASoftechDB dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task AddAsync(Order order, CancellationToken cancellationToken = default)
        {
            await _dbContext.Orders.AddAsync(order, cancellationToken);
        }

        public async Task<Order?> GetByIdAsync(int orderId, CancellationToken cancellationToken = default)
        {
            return await _dbContext.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.OrderId == orderId, cancellationToken);
        }

        public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}

