namespace DNASoftech.Application.DTOs.Checkout
{
    public class CheckoutResponseDto
    {
        public int OrderId { get; set; }
        public string OrderStatus { get; set; } = string.Empty;
        public string PaymentStatus { get; set; } = string.Empty;
        public string? PaymentReference { get; set; }
    }
}

