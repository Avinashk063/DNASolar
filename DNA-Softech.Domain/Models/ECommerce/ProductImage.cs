using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DNASoftech.Domain.Models.ECommerce
{
    public class ProductImage
    {
        [Key]
        public int ProductImageId { get; set; }
        
        [ForeignKey(nameof(Product))]
        public int ProductId { get; set; }
        
        public string ImageUrl { get; set; } = null!;
        public byte[]? ImageData { get; set; }
        public string? ImageMimeType { get; set; }
        public int SortOrder { get; set; }

        public Product? Product { get; set; }
    }
}
