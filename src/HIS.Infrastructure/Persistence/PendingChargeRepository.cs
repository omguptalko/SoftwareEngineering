using Dapper;
using HIS.Application.Abstractions;
using HIS.Domain.Entities;

namespace HIS.Infrastructure.Persistence;

/// <summary>
/// Accrued-but-unbilled charges (billing Phase 2). Charges live in the FY data DB;
/// the doctor fee is resolved from the master DB (Doctor.ConsultationFee, else the
/// OPD-CONS tariff) and snapshotted onto the charge so later fee edits don't rewrite it.
/// </summary>
public sealed class PendingChargeRepository : IPendingChargeRepository
{
    private readonly ITenantConnectionFactory _f;
    public PendingChargeRepository(ITenantConnectionFactory f) => _f = f;

    public async Task<decimal?> AccrueDoctorFeeAsync(int? branchId, long patientId, long? admissionId, int doctorId, string source, CancellationToken ct = default)
    {
        // Resolve the doctor's fee + label from the master DB.
        decimal? fee; string name, dept; int? tariffId = null;
        using (var m = await _f.OpenMasterAsync(ct))
        {
            var doc = await m.QuerySingleOrDefaultAsync<(decimal? ConsultationFee, string Name, string Department)>(new CommandDefinition(
                "SELECT ConsultationFee, Name, Department FROM master.Doctor WHERE DoctorId = @doctorId",
                new { doctorId }, cancellationToken: ct));
            if (doc.Name is null) return null;   // unknown doctor
            name = doc.Name; dept = doc.Department; fee = doc.ConsultationFee;

            if (fee is null)
            {
                // Fallback to a consultation tariff: prefer one whose name matches the doctor's
                // department (e.g. "Consultation - Cardiology"), else any generic consultation/
                // OPD-CONS row; branch-specific wins over global. Works across tenant seeds.
                var t = await m.QuerySingleOrDefaultAsync<(int TariffId, decimal Rate)?>(new CommandDefinition(
                    @"SELECT TOP 1 TariffId, Rate FROM master.Tariff
                       WHERE IsActive = 1 AND (BranchId IS NULL OR BranchId = @branchId)
                         AND (ServiceCode = 'OPD-CONS' OR ServiceName LIKE '%Consultation%')
                       ORDER BY CASE WHEN @dept <> '' AND ServiceName LIKE '%' + @dept + '%' THEN 0 ELSE 1 END,
                                CASE WHEN BranchId = @branchId THEN 0 ELSE 1 END, TariffId",
                    new { branchId, dept = dept ?? "" }, cancellationToken: ct));
                if (t is null) return null;   // no fee and no fallback → nothing to accrue
                fee = t.Value.Rate; tariffId = t.Value.TariffId;
            }
        }

        var desc = $"Consultation — {name}" + (string.IsNullOrWhiteSpace(dept) ? "" : $" ({dept})");
        using var c = await _f.OpenDataAsync(ct);
        const string sql = @"
INSERT INTO billing.PendingCharge (BranchId, PatientId, AdmissionId, Source, Description, DoctorId, TariffId, Qty, Rate)
VALUES (@branchId, @patientId, @admissionId, @source, @desc, @doctorId, @tariffId, 1, @fee);";
        await c.ExecuteAsync(new CommandDefinition(sql,
            new { branchId, patientId, admissionId, source, desc, doctorId, tariffId, fee }, cancellationToken: ct));
        return fee;
    }

    public async Task<IReadOnlyList<PendingCharge>> GetUnbilledForBillAsync(long patientId, long? admissionId, CancellationToken ct = default)
    {
        using var c = await _f.OpenDataAsync(ct);
        // Admission bill pulls that admission's charges; a plain patient bill pulls OPD (unlinked) charges.
        var sql = admissionId.HasValue
            ? "SELECT * FROM billing.PendingCharge WHERE PatientId = @patientId AND AdmissionId = @admissionId AND BilledBillId IS NULL ORDER BY ChargeId"
            : "SELECT * FROM billing.PendingCharge WHERE PatientId = @patientId AND AdmissionId IS NULL AND BilledBillId IS NULL ORDER BY ChargeId";
        return (await c.QueryAsync<PendingCharge>(new CommandDefinition(sql, new { patientId, admissionId }, cancellationToken: ct))).ToList();
    }

    public async Task<IReadOnlyList<PendingCharge>> GetUnbilledByPatientAsync(long patientId, CancellationToken ct = default)
    {
        using var c = await _f.OpenDataAsync(ct);
        return (await c.QueryAsync<PendingCharge>(new CommandDefinition(
            "SELECT * FROM billing.PendingCharge WHERE PatientId = @patientId AND BilledBillId IS NULL ORDER BY ChargeId",
            new { patientId }, cancellationToken: ct))).ToList();
    }

    public async Task MarkBilledAsync(IEnumerable<long> chargeIds, long billId, CancellationToken ct = default)
    {
        var ids = chargeIds.Distinct().ToArray();
        if (ids.Length == 0) return;
        using var c = await _f.OpenDataAsync(ct);
        await c.ExecuteAsync(new CommandDefinition(
            "UPDATE billing.PendingCharge SET BilledBillId = @billId WHERE ChargeId IN @ids AND BilledBillId IS NULL",
            new { billId, ids }, cancellationToken: ct));
    }
}
