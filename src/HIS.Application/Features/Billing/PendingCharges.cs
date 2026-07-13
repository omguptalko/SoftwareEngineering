using HIS.Application.Abstractions;
using HIS.Application.Features.Appointments;   // LookupCode

namespace HIS.Application.Features.Billing;

// Billing Phase 2 — show a patient's accrued-but-unbilled charges (doctor fees, etc.)
// so staff can see what CreateBill will auto-pull into the bill.
public sealed record PendingChargeDto(long ChargeId, string Source, string Description, decimal Amount, long? AdmissionId, string CreatedUtc);

public sealed record GetPendingChargesQuery(string PatientUhid) : IQuery<IReadOnlyList<PendingChargeDto>>, IRequireAuthentication;

public sealed class GetPendingChargesHandler : MediatR.IRequestHandler<GetPendingChargesQuery, IReadOnlyList<PendingChargeDto>>
{
    private readonly IPendingChargeRepository _pending;
    private readonly IPatientRepository _patients;
    public GetPendingChargesHandler(IPendingChargeRepository pending, IPatientRepository patients)
    { _pending = pending; _patients = patients; }

    public async Task<IReadOnlyList<PendingChargeDto>> Handle(GetPendingChargesQuery q, CancellationToken ct)
    {
        var patient = await _patients.GetByUhidAsync(LookupCode.Parse(q.PatientUhid), ct);
        if (patient is null) return Array.Empty<PendingChargeDto>();
        var rows = await _pending.GetUnbilledByPatientAsync(patient.PatientId, ct);
        return rows.Select(r => new PendingChargeDto(
            r.ChargeId, r.Source, r.Description, r.Amount, r.AdmissionId,
            r.CreatedUtc.ToString("yyyy-MM-dd HH:mm"))).ToList();
    }
}
