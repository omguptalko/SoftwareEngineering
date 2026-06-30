using Microsoft.AspNetCore.SignalR;

namespace HIS.Api.RealTime;

/// <summary>
/// Hospital-wide real-time alerts hub (SRS 3.5 / parent task 0.9). Any connected
/// client (ED board, nursing stations, dashboards) listens for "emergencyAlert";
/// the server pushes it from <c>EmergencyController</c> the moment a triage case is
/// registered, so a critical arrival is broadcast instantly without polling.
/// </summary>
public sealed class AlertsHub : Hub
{
}
