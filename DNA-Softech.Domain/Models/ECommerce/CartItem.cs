using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DNASoftech.Domain.Models.ECommerce
{
    public class CartItem
    {
        [Key]
        public int CartItemId { get; set; }

        [ForeignKey(nameof(Cart))]
        public int CartId { get; set; }

        [ForeignKey(nameof(Product))]
        public int ProductId { get; set; }

        public int Quantity { get; set; }

        public Cart? Cart { get; set; }

        public Product? Product { get; set; }
    }
}

