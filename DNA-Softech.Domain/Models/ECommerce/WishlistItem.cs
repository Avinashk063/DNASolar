using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DNASoftech.Domain.Models.ECommerce
{
    public class WishlistItem
    {
        [Key]
        public int WishlistItemId { get; set; }
        
        public string UserId { get; set; } = null!;
        
        [ForeignKey(nameof(Product))]
        public int ProductId { get; set; }
        
        public Product? Product { get; set; }

        public static WishlistItem Create(string userId, int productId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new ArgumentException("UserId is required.", nameof(userId));
            }

            if (productId <= 0)
            {
                throw new ArgumentException("ProductId must be greater than zero.", nameof(productId));
            }

            return new WishlistItem
            {
                UserId = userId.Trim(),
                ProductId = productId
            };
        }
    }
}
