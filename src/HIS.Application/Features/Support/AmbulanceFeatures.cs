using FluentValidation;
using HIS.Application.Abstractions;
using HIS.Domain.Entities;
using HIS.Shared.Context;

namespace HIS.Application.Features.Support;

public sealed record AmbulanceDto(int AmbulanceId, string VehicleNo, string Status);
public sealed record GetAmbulancesQuery : IQuery<IReadOnlyList<AmbulanceDto>>;

/// <summary>Add a vehicle to the fleet (starts 'Available'). SRS §3.6.</summary>
public sealed record AddAmbulanceCommand(string VehicleNo) : ICommand<AmbulanceDto>, IAuditable
{
    public string AuditEntity => "Ambulance";
    public string? AuditEntityId => VehicleNo;
}
public sealed class AddAmbulanceValidator : AbstractValidator<AddAmbulanceCommand>
{
    public AddAmbulanceValidator() => RuleFor(x => x.VehicleNo).NotEmpty().MaximumLength(20);
}
public sealed class AddAmbulanceHandler : MediatR.IRequestHandler<AddAmbulanceCommand, AmbulanceDto>
{
    private readonly IAmbulanceRepository _amb; private readonly IBranchContext _ctx;
    public AddAmbulanceHandler(IAmbulanceRepository amb, IBranchContext ctx) { _amb = amb; _ctx = ctx; }
    public async Task<AmbulanceDto> Handle(AddAmbulanceCommand c, CancellationToken ct)
    {
        var branchId = _ctx.BranchId ?? throw new InvalidOperationException("Branch context required.");
        var vehicleNo = c.VehicleNo.Trim();
        if (await _amb.VehicleExistsAsync(branchId, vehicleNo, ct))
            throw new InvalidOperationException($"Vehicle '{vehicleNo}' already exists in the fleet.");
        var id = await _amb.InsertAmbulanceAsync(branchId, vehicleNo, ct);
        return new AmbulanceDto(id, vehicleNo, "Available");
    }
}

public sealed class GetAmbulancesHandler : MediatR.IRequestHandler<GetAmbulancesQuery, IReadOnlyList<AmbulanceDto>>
{
    private readonly IAmbulanceRepository _amb; private readonly IBranchContext _ctx;
    public GetAmbulancesHandler(IAmbulanceRepository amb, IBranchContext ctx) { _amb = amb; _ctx = ctx; }
    public async Task<IReadOnlyList<AmbulanceDto>> Handle(GetAmbulancesQuery q, CancellationToken ct)
        => (await _amb.GetAmbulancesAsync(_ctx.BranchId ?? 0, ct)).Select(a => new AmbulanceDto(a.AmbulanceId, a.VehicleNo, a.Status)).ToList();
}

/// <summary>Log an emergency call and dispatch the nearest available ambulance — SRS §3.6.</summary>
public sealed record DispatchAmbulanceCommand(decimal? PickupLat, decimal? PickupLng) : ICommand<DispatchAmbulanceResult>, IAuditable
{
    public string AuditEntity => "AmbulanceDispatch";
    public string? AuditEntityId => null;
}
public sealed record DispatchAmbulanceResult(long DispatchId, int AmbulanceId);

public sealed class DispatchAmbulanceHandler : MediatR.IRequestHandler<DispatchAmbulanceCommand, DispatchAmbulanceResult>
{
    private readonly IAmbulanceRepository _amb; private readonly IBranchContext _ctx;
    public DispatchAmbulanceHandler(IAmbulanceRepository amb, IBranchContext ctx) { _amb = amb; _ctx = ctx; }

    public async Task<DispatchAmbulanceResult> Handle(DispatchAmbulanceCommand c, CancellationToken ct)
    {
        var branchId = _ctx.BranchId ?? throw new InvalidOperationException("Branch context required.");
        var ambulanceId = await _amb.GetFirstAvailableAsync(branchId, ct)
            ?? throw new InvalidOperationException("No ambulance available.");
        var dispatchId = await _amb.InsertDispatchAsync(new AmbulanceDispatch
        {
            AmbulanceId = ambulanceId, CallLoggedUtc = DateTime.UtcNow,
            PickupLat = c.PickupLat, PickupLng = c.PickupLng, LastLat = c.PickupLat, LastLng = c.PickupLng, Status = "Dispatched"
        }, ct);
        await _amb.SetAmbulanceStatusAsync(ambulanceId, "Dispatched", ct);
        return new DispatchAmbulanceResult(dispatchId, ambulanceId);
    }
}

public sealed record ArriveDispatchCommand(long DispatchId, decimal? Lat, decimal? Lng) : ICommand<bool>, IAuditable
{
    public string AuditEntity => "AmbulanceDispatch";
    public string? AuditEntityId => DispatchId.ToString();
}

public sealed class ArriveDispatchHandler : MediatR.IRequestHandler<ArriveDispatchCommand, bool>
{
    private readonly IAmbulanceRepository _amb;
    public ArriveDispatchHandler(IAmbulanceRepository amb) { _amb = amb; }
    public async Task<bool> Handle(ArriveDispatchCommand c, CancellationToken ct)
    {
        var ambulanceId = await _amb.GetDispatchAmbulanceAsync(c.DispatchId, ct);
        await _amb.ArriveAsync(c.DispatchId, c.Lat, c.Lng, ct);
        await _amb.SetAmbulanceStatusAsync(ambulanceId, "Available", ct);
        return true;
    }
}

public sealed record DispatchRowDto(long DispatchId, string Vehicle, string Logged, string? Arrived, string Status);
public sealed record GetDispatchesQuery : IQuery<IReadOnlyList<DispatchRowDto>>;

public sealed class GetDispatchesHandler : MediatR.IRequestHandler<GetDispatchesQuery, IReadOnlyList<DispatchRowDto>>
{
    private readonly IAmbulanceRepository _amb; private readonly IBranchContext _ctx;
    public GetDispatchesHandler(IAmbulanceRepository amb, IBranchContext ctx) { _amb = amb; _ctx = ctx; }
    public async Task<IReadOnlyList<DispatchRowDto>> Handle(GetDispatchesQuery q, CancellationToken ct)
        => (await _amb.GetDispatchesAsync(_ctx.BranchId ?? 0, ct)).Select(d => new DispatchRowDto(d.DispatchId, d.Vehicle, d.Logged, d.Arrived, d.Status)).ToList();
}
