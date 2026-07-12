using HIS.Application.Abstractions;
using Microsoft.Extensions.Configuration;

namespace HIS.Application.Features.Billing;

// =====================================================================
// Payment Gateway console (SRS §5). Read-only monitoring surface over the
// billing.Payment ledger + the configured gateway provider. Provider name,
// environment and supported modes are config-driven (Key Vault in prod) —
// switching provider is never a code change.
// =====================================================================

// ---- Gateway status (provider / environment / modes) ----------------
public sealed record GatewayStatusDto(string Provider, string Environment, bool IsSandbox, IReadOnlyList<string> Modes);
public sealed record GetGatewayStatusQuery : IQuery<GatewayStatusDto>, IRequireAuthentication;

public sealed class GetGatewayStatusHandler : MediatR.IRequestHandler<GetGatewayStatusQuery, GatewayStatusDto>
{
    private readonly IConfiguration _config;
    public GetGatewayStatusHandler(IConfiguration config) => _config = config;

    public Task<GatewayStatusDto> Handle(GetGatewayStatusQuery q, CancellationToken ct)
    {
        var provider = _config["Payments:ActiveProvider"] ?? "Sandbox";
        // This build wires the SandboxPaymentGateway adapter; a real adapter would flip this via config.
        var env = _config["Payments:Environment"] ?? "Sandbox";
        var isSandbox = !string.Equals(env, "Live", StringComparison.OrdinalIgnoreCase);
        var modes = (_config["Payments:Modes"] ?? "UPI,Card,NetBanking,QR,Cash")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return Task.FromResult(new GatewayStatusDto(provider, env, isSandbox, modes));
    }
}

// ---- Transactions ledger --------------------------------------------
public sealed record GatewayTxnDto(long PaymentId, string? BillNo, string Patient, string Mode, string? Gateway,
    decimal Amount, string? Reference, string Status, string CreatedUtc);
public sealed record GetGatewayTransactionsQuery(int Take = 100) : IQuery<IReadOnlyList<GatewayTxnDto>>, IRequireAuthentication;

public sealed class GetGatewayTransactionsHandler : MediatR.IRequestHandler<GetGatewayTransactionsQuery, IReadOnlyList<GatewayTxnDto>>
{
    private readonly IBillingRepository _billing;
    public GetGatewayTransactionsHandler(IBillingRepository billing) => _billing = billing;

    public async Task<IReadOnlyList<GatewayTxnDto>> Handle(GetGatewayTransactionsQuery q, CancellationToken ct)
        => (await _billing.GetPaymentsAsync(q.Take, ct)).Select(p => new GatewayTxnDto(
            p.PaymentId, p.BillNo, p.Patient, p.Mode, p.Gateway, p.Amount, p.GatewayRef, p.Status,
            p.CreatedUtc.ToString("yyyy-MM-dd HH:mm"))).ToList();
}
