namespace DNASoftech.Application.DTOs.Wishlist
{
    public class AddWishlistItemRequestDto
    {
        public string UserId { get; set; } = string.Empty;
        public int ProductId { get; set; }
    }
}
