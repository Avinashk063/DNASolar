namespace DNASoftech.Application.DTOs.Checkout
{
    public class CheckoutRequestDto
    {
        public string UserId { get; set; } = string.Empty;
        public CheckoutCustomerDto Customer { get; set; } = new();
        public CheckoutPaymentDto Payment { get; set; } = new();
    }
}

