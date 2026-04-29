using System.ComponentModel.DataAnnotations;

namespace DNASoftech.Domain.Models.ECommerce
{
    public class Category
    {
        [Key]
        public int CategoryId { get; set; }
        [Required]
        public string Name { get; set; } = null!;
    }
}
