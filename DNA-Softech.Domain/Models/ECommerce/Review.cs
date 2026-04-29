using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DNASoftech.Domain.Models.ECommerce
{
    /// <summary>
    /// Customer product review submitted from the shop product detail page.
    /// </summary>
    public class Review
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ProductId { get; set; }

        [ForeignKey(nameof(ProductId))]
        public Product? Product { get; set; }

        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        /// <summary>Rating 1-5.</summary>
        public int Rating { get; set; }

        [Required]
        public string Text { get; set; } = string.Empty;

        public bool VerifiedPurchase { get; set; }

        /// <summary>Optional review photo stored as a base64 data URI.</summary>
        public string? MediaImageData { get; set; }

        /// <summary>Optional review video stored as a base64 data URI (10-15 sec).</summary>
        public string? MediaVideoData { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
