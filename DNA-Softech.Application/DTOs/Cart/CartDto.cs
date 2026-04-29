namespace DNASoftech.Application.DTOs.Cart
{
    public class CartDto
    {
        public string UserId { get; set; } = string.Empty;
        public IReadOnlyCollection<CartItemDto> Items { get; set; } = Array.Empty<CartItemDto>();
        public decimal Total => Items.Sum(i => i.LineTotal);
    }
}

