namespace DNASoftech.Application.DTOs.Checkout
{
    public class CheckoutPaymentDto
    {
        public string Method { get; set; } = string.Empty;
        public string? Details { get; set; }
    }
}

