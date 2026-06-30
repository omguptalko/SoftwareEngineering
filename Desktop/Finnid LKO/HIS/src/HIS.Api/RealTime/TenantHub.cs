using HIS.Shared.Context;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;

namespace HIS.Api.RealTime;

/// <summary>
/// Base hub that joins each new connection to its resolved tenant's group (task 0.9).
/// The tenant is read from the connection's HTTP request scope — where
/// TenantResolutionMiddleware populated <see cref="ITenantContext"/> (host /
/// common-domain subdomain / X-Tenant). NOTE: a hub's own constructor-injected scope is
/// NOT the connection request scope, so we resolve via <c>GetHttpContext().RequestServices</c>.
/// Broadcasts then go to <c>Clients.Group(TenantGroups.Name(...))</c>, reaching only that tenant.
/// </summary>
public abstract class TenantHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var tenant = Context.GetHttpContext()?.RequestServices.GetService<ITenantContext>();
        var group = tenant is not null ? TenantGroups.Name(tenant) : "tenant:_unresolved";
        await Groups.AddToGroupAsync(Context.ConnectionId, group);
        await base.OnConnectedAsync();
    }
}
