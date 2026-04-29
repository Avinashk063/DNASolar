namespace DNASoftech.Application.DTOs.Cart
{
    public class UpsertCartItemRequestDto
    {
        public string UserId { get; set; } = string.Empty;
        public int ProductId { get; set; }
        public int Quantity { get; set; }
    }
}

