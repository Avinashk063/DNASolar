namespace DNASoftech.Application.DTOs.Payment
{
    public class PaymentResultDto
    {
        public bool Success { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? TransactionId { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? RedirectUrl { get; set; }
        public Dictionary<string, string>? PaymentData { get; set; }
    }
}

