namespace HIS.Api.RealTime;

/// <summary>
/// Tenant-wide real-time alerts hub (SRS 3.5 / parent task 0.9). Any connected client
/// (ED board, nursing stations, dashboards) listens for "emergencyAlert"; the server
/// pushes it from <c>EmergencyController</c> to the tenant group the moment a triage case
/// is registered, so a critical arrival is broadcast instantly within that tenant.
/// </summary>
public sealed class AlertsHub : TenantHub
{
}
