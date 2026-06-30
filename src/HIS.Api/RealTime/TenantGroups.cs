using HIS.Shared.Context;

namespace HIS.Api.RealTime;

/// <summary>
/// Per-tenant SignalR group naming (task 0.9 isolation). Every real-time broadcast is
/// scoped to the resolved tenant so a push never crosses tenant boundaries — the
/// real-time mirror of the per-tenant DB isolation (L1.9.2). Unresolved connections
/// share a separate bucket and never receive a tenant's messages.
/// </summary>
public static class TenantGroups
{
    public static string Name(ITenantContext tenant) =>
        tenant.IsResolved ? $"tenant:{tenant.TenantCode}" : "tenant:_unresolved";
}
