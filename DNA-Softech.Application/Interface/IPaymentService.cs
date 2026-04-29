using DNASoftech.Application.DTOs.Payment;

namespace DNASoftech.Application.Interface
{
    public interface IPaymentService
    {
        Task<PaymentResultDto> ProcessPaymentAsync(PaymentRequestDto request, CancellationToken cancellationToken = default);
    }
}

