namespace DNASoftech.Domain.Models.Settings
{
    public class PaymentGatewaySettings
    {
        public string Provider { get; set; } = "Sandbox";
        public bool AutoApprovePayments { get; set; } = true;
        public string? MerchantKey { get; set; }
        public string? Salt { get; set; }
        public string? BaseUrl { get; set; }
    }
}

