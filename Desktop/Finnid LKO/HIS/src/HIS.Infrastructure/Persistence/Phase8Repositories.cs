using Dapper;
using HIS.Application.Abstractions;
using HIS.Domain.Entities;

namespace HIS.Infrastructure.Persistence;

// L1.8.5 cutover: Staff master is longitudinal (master DB); attendance/leave/payroll are
// fiscal-scoped (FY DB). Lists that filter/sort by staff branch+name use the app-side
// two-step pattern (D8): fetch the branch's staff from master, join the FY rows in C#.
public sealed class HrRepository : IHrRepository
{
    private readonly ITenantConnectionFactory _f;
    public HrRepository(ITenantConnectionFactory f) => _f = f;

    public async Task<long?> GetStaffIdByCodeAsync(int branchId, string code, CancellationToken ct = default)
    {
        using var c = await _f.OpenMasterAsync(ct);
        return await c.QuerySingleOrDefaultAsync<long?>(new CommandDefinition(
            "SELECT StaffId FROM master.Staff WHERE BranchId = @branchId AND EmployeeCode = @code AND IsActive = 1",
            new { branchId, code }, cancellationToken: ct));
    }

    public async Task<long> InsertStaffAsync(Staff s, CancellationToken ct = default)
    {
        using var c = await _f.OpenMasterAsync(ct);
        const string sql = @"
INSERT INTO master.Staff (BranchId, EmployeeCode, FullName, Designation, Department, DateOfJoining, IsActive)
VALUES (@BranchId, @EmployeeCode, @FullName, @Designation, @Department, @DateOfJoining, @IsActive);
SELECT CAST(SCOPE_IDENTITY() AS BIGINT);";
        return await c.QuerySingleAsync<long>(new CommandDefinition(sql, s, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<Staff>> GetStaffAsync(int branchId, CancellationToken ct = default)
    {
        using var c = await _f.OpenMasterAsync(ct);
        return (await c.QueryAsync<Staff>(new CommandDefinition(
            "SELECT * FROM master.Staff WHERE BranchId = @branchId AND IsActive = 1 ORDER BY FullName",
            new { branchId }, cancellationToken: ct))).ToList();
    }

    public async Task UpsertAttendanceAsync(Attendance a, CancellationToken ct = default)
    {
        using var c = await _f.OpenDataAsync(ct);
        var updated = await c.ExecuteAsync(new CommandDefinition(
            @"UPDATE hr.Attendance SET Status = @Status, InTime = @InTime, OutTime = @OutTime
              WHERE StaffId = @StaffId AND WorkDate = @WorkDate", a, cancellationToken: ct));
        if (updated == 0)
            await c.ExecuteAsync(new CommandDefinition(
                @"INSERT INTO hr.Attendance (StaffId, WorkDate, InTime, OutTime, Status)
                  VALUES (@StaffId, @WorkDate, @InTime, @OutTime, @Status);", a, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<(string, string, string, string?, string?)>> GetAttendanceAsync(int branchId, DateTime date, CancellationToken ct = default)
    {
        var staff = await MasterLookup.StaffInBranchAsync(_f, branchId, ct);
        using var c = await _f.OpenDataAsync(ct);
        var att = await c.QueryAsync<(long StaffId, string Status, string? InTime, string? OutTime)>(new CommandDefinition(
            @"SELECT StaffId, Status, CONVERT(varchar(5), InTime, 108) AS InTime, CONVERT(varchar(5), OutTime, 108) AS OutTime
              FROM hr.Attendance WHERE WorkDate = @date", new { date = date.Date }, cancellationToken: ct));
        return att.Where(a => staff.ContainsKey(a.StaffId))
                  .Select(a => (staff[a.StaffId].Code, staff[a.StaffId].Name, a.Status, a.InTime, a.OutTime))
                  .OrderBy(r => r.Item2).ToList();
    }

    public async Task<long> InsertLeaveAsync(LeaveRequest l, CancellationToken ct = default)
    {
        using var c = await _f.OpenDataAsync(ct);
        const string sql = @"
INSERT INTO hr.LeaveRequest (StaffId, FromDate, ToDate, LeaveType, Status)
VALUES (@StaffId, @FromDate, @ToDate, @LeaveType, @Status);
SELECT CAST(SCOPE_IDENTITY() AS BIGINT);";
        return await c.QuerySingleAsync<long>(new CommandDefinition(sql, l, cancellationToken: ct));
    }

    public async Task SetLeaveStatusAsync(long leaveId, string status, CancellationToken ct = default)
    {
        using var c = await _f.OpenDataAsync(ct);
        await c.ExecuteAsync(new CommandDefinition(
            "UPDATE hr.LeaveRequest SET Status = @status WHERE LeaveId = @leaveId", new { leaveId, status }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<(long, string, string, string, string?, string)>> GetLeavesAsync(int branchId, CancellationToken ct = default)
    {
        var staff = await MasterLookup.StaffInBranchAsync(_f, branchId, ct);
        using var c = await _f.OpenDataAsync(ct);
        var leaves = await c.QueryAsync<(long LeaveId, long StaffId, string FromDate, string ToDate, string? LeaveType, string Status)>(new CommandDefinition(
            @"SELECT LeaveId, StaffId, CONVERT(varchar(10), FromDate, 105) AS FromDate,
                     CONVERT(varchar(10), ToDate, 105) AS ToDate, LeaveType, Status
              FROM hr.LeaveRequest ORDER BY LeaveId DESC", cancellationToken: ct));
        return leaves.Where(l => staff.ContainsKey(l.StaffId))
                     .Select(l => (l.LeaveId, staff[l.StaffId].Name, l.FromDate, l.ToDate, l.LeaveType, l.Status)).ToList();
    }
}

public sealed class PayrollRepository : IPayrollRepository
{
    private readonly ITenantConnectionFactory _f;
    public PayrollRepository(ITenantConnectionFactory f) => _f = f;

    public async Task<long?> GetStaffIdByCodeAsync(int branchId, string code, CancellationToken ct = default)
    {
        using var c = await _f.OpenMasterAsync(ct);
        return await c.QuerySingleOrDefaultAsync<long?>(new CommandDefinition(
            "SELECT StaffId FROM master.Staff WHERE BranchId = @branchId AND EmployeeCode = @code AND IsActive = 1",
            new { branchId, code }, cancellationToken: ct));
    }

    public async Task<long> UpsertRunAsync(PayrollRun r, CancellationToken ct = default)
    {
        using var c = await _f.OpenDataAsync(ct);
        var existing = await c.QuerySingleOrDefaultAsync<long?>(new CommandDefinition(
            "SELECT PayrollId FROM hr.PayrollRun WHERE StaffId = @StaffId AND PeriodYear = @PeriodYear AND PeriodMonth = @PeriodMonth",
            r, cancellationToken: ct));
        if (existing is long id)
        {
            await c.ExecuteAsync(new CommandDefinition(
                @"UPDATE hr.PayrollRun SET BasicPay = @BasicPay, OvertimeHours = @OvertimeHours, OvertimeAmount = @OvertimeAmount,
                         GrossPay = @GrossPay, NetPay = @NetPay, Status = 'Draft', OvertimeApprovedBy = NULL
                  WHERE PayrollId = @id",
                new { r.BasicPay, r.OvertimeHours, r.OvertimeAmount, r.GrossPay, r.NetPay, id }, cancellationToken: ct));
            return id;
        }
        const string sql = @"
INSERT INTO hr.PayrollRun (StaffId, PeriodYear, PeriodMonth, BasicPay, OvertimeHours, OvertimeAmount, GrossPay, NetPay, Status)
VALUES (@StaffId, @PeriodYear, @PeriodMonth, @BasicPay, @OvertimeHours, @OvertimeAmount, @GrossPay, @NetPay, @Status);
SELECT CAST(SCOPE_IDENTITY() AS BIGINT);";
        return await c.QuerySingleAsync<long>(new CommandDefinition(sql, r, cancellationToken: ct));
    }

    public async Task<PayrollRun?> GetRunAsync(long payrollId, CancellationToken ct = default)
    {
        using var c = await _f.OpenDataAsync(ct);
        return await c.QuerySingleOrDefaultAsync<PayrollRun>(new CommandDefinition(
            "SELECT * FROM hr.PayrollRun WHERE PayrollId = @payrollId", new { payrollId }, cancellationToken: ct));
    }

    public async Task ApproveOvertimeAsync(long payrollId, long approvedBy, CancellationToken ct = default)
    {
        using var c = await _f.OpenDataAsync(ct);
        await c.ExecuteAsync(new CommandDefinition(
            "UPDATE hr.PayrollRun SET OvertimeApprovedBy = @approvedBy, Status = 'Approved' WHERE PayrollId = @payrollId",
            new { payrollId, approvedBy }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<(long, string, string, decimal, decimal, decimal, decimal, string)>> GetRunsAsync(int branchId, int year, int month, CancellationToken ct = default)
    {
        var staff = await MasterLookup.StaffInBranchAsync(_f, branchId, ct);
        using var c = await _f.OpenDataAsync(ct);
        var runs = await c.QueryAsync<(long PayrollId, long StaffId, decimal BasicPay, decimal OvertimeHours, decimal OvertimeAmount, decimal NetPay, string Status)>(new CommandDefinition(
            @"SELECT PayrollId, StaffId, BasicPay, OvertimeHours, OvertimeAmount, NetPay, Status
              FROM hr.PayrollRun WHERE PeriodYear = @year AND PeriodMonth = @month", new { year, month }, cancellationToken: ct));
        return runs.Where(p => staff.ContainsKey(p.StaffId))
                   .Select(p => (p.PayrollId, staff[p.StaffId].Code, staff[p.StaffId].Name, p.BasicPay, p.OvertimeHours, p.OvertimeAmount, p.NetPay, p.Status))
                   .OrderBy(r => r.Item3).ToList();
    }
}
