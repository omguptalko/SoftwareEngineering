using HIS.Application.Abstractions;
using HIS.Domain.Entities;
using HIS.Shared.Context;

namespace HIS.Application.Features.Icu;

// ============================ ICU census (who to monitor) — §3.6 ============================
public sealed record IcuAdmissionDto(long AdmissionId, string Patient, string Uhid, string Ward, string BedNo, string? Consultant);
public sealed record GetIcuAdmissionsQuery : IQuery<IReadOnlyList<IcuAdmissionDto>>;

public sealed class GetIcuAdmissionsHandler : MediatR.IRequestHandler<GetIcuAdmissionsQuery, IReadOnlyList<IcuAdmissionDto>>
{
    private readonly IIcuRepository _icu;
    private readonly IBranchContext _ctx;
    public GetIcuAdmissionsHandler(IIcuRepository icu, IBranchContext ctx) { _icu = icu; _ctx = ctx; }

    public async Task<IReadOnlyList<IcuAdmissionDto>> Handle(GetIcuAdmissionsQuery q, CancellationToken ct)
    {
        var rows = await _icu.GetIcuAdmissionsAsync(_ctx.BranchId ?? 0, ct);
        return rows.Select(r => new IcuAdmissionDto(r.Item1, r.Item2, r.Item3, r.Item4, r.Item5, r.Item6)).ToList();
    }
}

// ============================ Record an ICU observation (flowsheet entry) — §3.6 ============================
public sealed record RecordIcuObsCommand(
    long AdmissionId,
    int? HeartRate = null, int? BpSystolic = null, int? BpDiastolic = null, int? Map = null,
    int? Spo2 = null, int? RespRate = null, decimal? TempF = null,
    int? Cvp = null, int? EtCo2 = null, int? Fio2 = null, byte? GcsTotal = null, byte? PainScore = null,
    int? UrineOutputMl = null, int? BloodSugar = null, string? VentMode = null, string? Notes = null)
    : ICommand<long>, IAuditable, IAuthorizable
{
    public string AuditEntity => "IcuObservation";
    public string? AuditEntityId => AdmissionId.ToString();
    public string RequiredPermission => "icu.monitor";
}

public sealed class RecordIcuObsHandler : MediatR.IRequestHandler<RecordIcuObsCommand, long>
{
    private readonly IIcuRepository _icu;
    public RecordIcuObsHandler(IIcuRepository icu) { _icu = icu; }

    public Task<long> Handle(RecordIcuObsCommand c, CancellationToken ct)
    {
        // MAP auto-derives from BP when not supplied: (SBP + 2*DBP) / 3.
        var map = c.Map ?? (c.BpSystolic is int s && c.BpDiastolic is int d ? (s + 2 * d) / 3 : (int?)null);
        return _icu.InsertObservationAsync(new IcuObservation
        {
            AdmissionId = c.AdmissionId,
            RecordedUtc = DateTime.UtcNow,
            HeartRate = c.HeartRate, BpSystolic = c.BpSystolic, BpDiastolic = c.BpDiastolic, Map = map,
            Spo2 = c.Spo2, RespRate = c.RespRate, TempF = c.TempF,
            Cvp = c.Cvp, EtCo2 = c.EtCo2, Fio2 = c.Fio2, GcsTotal = c.GcsTotal, PainScore = c.PainScore,
            UrineOutputMl = c.UrineOutputMl, BloodSugar = c.BloodSugar, VentMode = c.VentMode, Notes = c.Notes
        }, ct);
    }
}

// ============================ ICU flowsheet (§3.6) ============================
public sealed record IcuObsDto(
    long IcuObservationId, DateTime RecordedUtc, int? HeartRate, int? BpSystolic, int? BpDiastolic, int? Map,
    int? Spo2, int? RespRate, decimal? TempF, int? Cvp, int? EtCo2, int? Fio2, byte? GcsTotal, byte? PainScore,
    int? UrineOutputMl, int? BloodSugar, string? VentMode, string? Notes);
public sealed record GetIcuFlowsheetQuery(long AdmissionId) : IQuery<IReadOnlyList<IcuObsDto>>;

public sealed class GetIcuFlowsheetHandler : MediatR.IRequestHandler<GetIcuFlowsheetQuery, IReadOnlyList<IcuObsDto>>
{
    private readonly IIcuRepository _icu;
    public GetIcuFlowsheetHandler(IIcuRepository icu) { _icu = icu; }

    public async Task<IReadOnlyList<IcuObsDto>> Handle(GetIcuFlowsheetQuery q, CancellationToken ct)
    {
        var rows = await _icu.GetFlowsheetAsync(q.AdmissionId, ct);
        return rows.Select(o => new IcuObsDto(
            o.IcuObservationId, o.RecordedUtc, o.HeartRate, o.BpSystolic, o.BpDiastolic, o.Map,
            o.Spo2, o.RespRate, o.TempF, o.Cvp, o.EtCo2, o.Fio2, o.GcsTotal, o.PainScore,
            o.UrineOutputMl, o.BloodSugar, o.VentMode, o.Notes)).ToList();
    }
}
