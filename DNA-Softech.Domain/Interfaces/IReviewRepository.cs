using DNASoftech.Domain.Models.ECommerce;

namespace DNASoftech.Domain.Interfaces
{
    public interface IReviewRepository
    {
        Task<ReviewSummary?> GetSummaryByProductIdAsync(int productId, CancellationToken cancellationToken = default);
        Task<IReadOnlyCollection<ReviewSummary>> GetSummariesByProductIdsAsync(IEnumerable<int> productIds, CancellationToken cancellationToken = default);
    }
}
