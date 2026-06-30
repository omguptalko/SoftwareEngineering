namespace HIS.Api.RealTime;

/// <summary>
/// Real-time queue / digital-signage hub (SRS 3.31, parent task 0.9). Clients
/// (counter screens, waiting-room signage) connect and listen for "queueChanged";
/// the server pushes that signal from <c>QueueController</c> to the tenant group
/// whenever a token is issued or called, so every board refreshes live without
/// polling. No state is held here — listeners re-fetch the board from /api/queue.
/// </summary>
public sealed class QueueHub : TenantHub
{
}
