namespace DNASoftech.Application.DTOs.Product
{
    public class ProductDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public decimal? OriginalPrice { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
        public bool InStock { get; set; }
        public int Discount { get; set; }
        public double Rating { get; set; }
        public int Reviews { get; set; }
    }
}
