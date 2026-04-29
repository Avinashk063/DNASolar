using DNASoftech.Application.DTOs.Checkout;

namespace DNASoftech.Application.Interface
{
    public interface ICheckoutService
    {
        Task<CheckoutResponseDto> CheckoutAsync(CheckoutRequestDto request, CancellationToken cancellationToken = default);
    }
}

