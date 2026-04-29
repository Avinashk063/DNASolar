namespace DNASoftech.Application.DTOs.Payment
{
    public class PaymentRequestDto
    {
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "INR";
        public string Method { get; set; } = string.Empty;
        public string? ReferenceData { get; set; }
        
        // PayU Requirements
        public string? FirstName { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? ProductInfo { get; set; }
        public string? SuccessUrl { get; set; }
        public string? FailureUrl { get; set; }
    }
}

