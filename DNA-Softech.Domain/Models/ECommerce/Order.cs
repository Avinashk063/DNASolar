using DNASoftech.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace DNASoftech.Domain.Models.ECommerce
{
    public class Order
    {
        [Key]
        public int OrderId { get; set; }

        [Required]
        [MaxLength(100)]
        public string UserId { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string CustomerName { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string CustomerEmail { get; set; } = string.Empty;

        [MaxLength(50)]
        public string CustomerPhone { get; set; } = string.Empty;

        [Required]
        public string CustomerAddress { get; set; } = string.Empty;

        public decimal TotalAmount { get; set; }

        public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Unknown;

        [MaxLength(500)]
        public string? PaymentReference { get; set; }

        public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Pending;

        public OrderStatus Status { get; set; } = OrderStatus.PendingConfirmation;

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        // ── Legacy compatibility fields ──────────────────────────────────────
        // ItemsJson stores the cart snapshot as JSON when full Order/OrderItem
        // normalisation is not used by the placing controller.
        public string? ItemsJson { get; set; }

        // PaymentDetails is a pipe-delimited bag used by the legacy checkout flow
        // (e.g. "status:PendingConfirmation|upiRef:xyz").
        public string? PaymentDetails { get; set; }

        public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();

        public void AddItem(int productId, int quantity, decimal unitPrice)
        {
            if (productId <= 0)
            {
                throw new ArgumentException("ProductId must be greater than zero.", nameof(productId));
            }

            if (quantity <= 0)
            {
                throw new ArgumentException("Quantity must be greater than zero.", nameof(quantity));
            }

            if (unitPrice < 0)
            {
                throw new ArgumentException("UnitPrice cannot be negative.", nameof(unitPrice));
            }

            Items.Add(new OrderItem
            {
                ProductId = productId,
                Quantity = quantity,
                UnitPrice = unitPrice
            });

            RecalculateTotals();
        }

        public void MarkAsPaid(string paymentReference)
        {
            if (string.IsNullOrWhiteSpace(paymentReference))
            {
                throw new ArgumentException("Payment reference is required.", nameof(paymentReference));
            }

            PaymentReference = paymentReference.Trim();
            PaymentStatus = PaymentStatus.Authorized;
            Status = OrderStatus.Confirmed;
        }

        public void MarkAsCancelled(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new ArgumentException("Cancellation reason is required.", nameof(reason));
            }

            Status = OrderStatus.Cancelled;
            PaymentDetails = string.IsNullOrWhiteSpace(PaymentDetails)
                ? $"cancelReason:{reason.Trim()}"
                : $"{PaymentDetails}|cancelReason:{reason.Trim()}";
        }

        public void RecalculateTotals()
        {
            TotalAmount = Items.Sum(i => i.UnitPrice * i.Quantity);
        }
    }
}

