namespace DNASoftech.Application.DTOs.Wishlist
{
    public class WishlistItemDto
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public decimal UnitPrice { get; set; }
    }

    public class WishlistDto
    {
        public string UserId { get; set; } = string.Empty;
        public List<WishlistItemDto> Items { get; set; } = new();
    }
}
