namespace HIS.Application.Abstractions;

public sealed record GatewayChargeRequest(decimal Amount, string Mode);
public sealed record GatewayChargeResult(bool Success, string Provider, string Reference, string Status);

/// <summary>
/// Payment gateway abstraction (SRS §5). The concrete provider (Razorpay/Stripe/
/// PayU/Cashfree) and its keys are resolved from config / Key Vault — switching
/// provider is a configuration change, never a code change.
/// </summary>
public interface IPaymentGateway
{
    Task<GatewayChargeResult> ChargeAsync(GatewayChargeRequest request, CancellationToken ct = default);
}
