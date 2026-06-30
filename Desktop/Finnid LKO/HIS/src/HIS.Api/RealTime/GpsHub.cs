namespace HIS.Api.RealTime;

/// <summary>
/// Live ambulance GPS tracking hub (SRS 3.6 / parent task 0.9). Vehicles post location
/// pings to <c>AmbulanceController</c>, which broadcasts "ambulanceMoved" to the tenant
/// group; the control room / dispatch board plots each vehicle live. Tracking is ephemeral
/// real-time state — nothing is persisted (a historical GPS trail would be a separate store).
/// </summary>
public sealed class GpsHub : TenantHub
{
}
