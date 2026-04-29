using System.ComponentModel.DataAnnotations;

namespace DNASoftech.Domain.Models.ECommerce
{
    public class Cart
    {
        [Key]
        public int CartId { get; set; }

        [Required]
        [MaxLength(100)]
        public string UserId { get; set; } = string.Empty;

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

        public ICollection<CartItem> Items { get; set; } = new List<CartItem>();

        public void AddItem(int productId, int quantity)
        {
            if (productId <= 0)
            {
                throw new ArgumentException("ProductId must be greater than zero.", nameof(productId));
            }

            if (quantity <= 0)
            {
                throw new ArgumentException("Quantity must be greater than zero.", nameof(quantity));
            }

            var existingItem = Items.FirstOrDefault(i => i.ProductId == productId);
            if (existingItem is null)
            {
                Items.Add(new CartItem { ProductId = productId, Quantity = quantity });
            }
            else
            {
                existingItem.Quantity = quantity;
            }

            UpdatedAtUtc = DateTime.UtcNow;
        }

        public void RemoveItem(int productId)
        {
            if (productId <= 0)
            {
                throw new ArgumentException("ProductId must be greater than zero.", nameof(productId));
            }

            var existingItem = Items.FirstOrDefault(i => i.ProductId == productId);
            if (existingItem is not null)
            {
                Items.Remove(existingItem);
                UpdatedAtUtc = DateTime.UtcNow;
            }
        }
    }
}

