using System.ComponentModel.DataAnnotations;

namespace DNASoftech.Domain.Models.ECommerce
{
    public class Product
    {
        [Key]
        public int ProductId { get; set; }

        [Required]
        [MaxLength(250)]
        public string Name { get; set; } = string.Empty;

        [Required]
        public string Description { get; set; } = string.Empty;

        [MaxLength(120)]
        public string Category { get; set; } = string.Empty;

        public decimal Price { get; set; }

        public decimal? OriginalPrice { get; set; }

        public string ImageUrl { get; set; } = string.Empty;

        public bool InStock { get; set; } = true;

        public int Discount { get; set; }

        public double Rating { get; set; }

        public int Reviews { get; set; }

        public byte[]? ImageData { get; set; }

        public string? ImageMimeType { get; set; }

        public ICollection<ProductImage> ProductImages { get; set; } = new List<ProductImage>();
    }
}

