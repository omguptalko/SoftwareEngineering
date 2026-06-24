using Dapper;
using HIS.Application.Abstractions;
using HIS.Domain.Entities;

namespace HIS.Infrastructure.Persistence;

public sealed class HrRepository : IHrRepository
{
    private readonly IDbConnectionFactory _f;
    public HrRepository(IDbConnectionFactory f) => _f = f;

    public async Task<long?> GetStaffIdByCodeAsync(int branchId, string code, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        return await c.QuerySingleOrDefaultAsync<long?>(new CommandDefinition(
            "SELECT StaffId FROM dbo.Staff WHERE BranchId = @branchId AND EmployeeCode = @code AND IsActive = 1",
            new { branchId, code }, cancellationToken: ct));
    }

    public async Task<long> InsertStaffAsync(Staff s, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        const string sql = @"
INSERT INTO dbo.Staff (BranchId, EmployeeCode, FullName, Designation, Department, DateOfJoining, IsActive)
VALUES (@BranchId, @EmployeeCode, @FullName, @Designation, @Department, @DateOfJoining, @IsActive);
SELECT CAST(SCOPE_IDENTITY() AS BIGINT);";
        return await c.QuerySingleAsync<long>(new CommandDefinition(sql, s, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<Staff>> GetStaffAsync(int branchId, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        return (await c.QueryAsync<Staff>(new CommandDefinition(
            "SELECT * FROM dbo.Staff WHERE BranchId = @branchId AND IsActive = 1 ORDER BY FullName",
            new { branchId }, cancellationToken: ct))).ToList();
    }

    public async Task UpsertAttendanceAsync(Attendance a, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        var updated = await c.ExecuteAsync(new CommandDefinition(
            @"UPDATE dbo.Attendance SET Status = @Status, InTime = @InTime, OutTime = @OutTime
              WHERE StaffId = @StaffId AND WorkDate = @WorkDate", a, cancellationToken: ct));
        if (updated == 0)
            await c.ExecuteAsync(new CommandDefinition(
                @"INSERT INTO dbo.Attendance (StaffId, WorkDate, InTime, OutTime, Status)
                  VALUES (@StaffId, @WorkDate, @InTime, @OutTime, @Status);", a, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<(string, string, string, string?, string?)>> GetAttendanceAsync(int branchId, DateTime date, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        var rows = await c.QueryAsync<(string, string, string, string?, string?)>(new CommandDefinition(
            @"SELECT s.EmployeeCode, s.FullName, a.Status,
                     CONVERT(varchar(5), a.InTime, 108) AS InTime,
                     CONVERT(varchar(5), a.OutTime, 108) AS OutTime
              FROM dbo.Attendance a INNER JOIN dbo.Staff s ON s.StaffId = a.StaffId
              WHERE s.BranchId = @branchId AND a.WorkDate = @date
              ORDER BY s.FullName", new { branchId, date = date.Date }, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<long> InsertLeaveAsync(LeaveRequest l, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        const string sql = @"
INSERT INTO dbo.LeaveRequest (StaffId, FromDate, ToDate, LeaveType, Status)
VALUES (@StaffId, @FromDate, @ToDate, @LeaveType, @Status);
SELECT CAST(SCOPE_IDENTITY() AS BIGINT);";
        return await c.QuerySingleAsync<long>(new CommandDefinition(sql, l, cancellationToken: ct));
    }

    public async Task SetLeaveStatusAsync(long leaveId, string status, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        await c.ExecuteAsync(new CommandDefinition(
            "UPDATE dbo.LeaveRequest SET Status = @status WHERE LeaveId = @leaveId", new { leaveId, status }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<(long, string, string, string, string?, string)>> GetLeavesAsync(int branchId, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        var rows = await c.QueryAsync<(long, string, string, string, string?, string)>(new CommandDefinition(
            @"SELECT l.LeaveId, s.FullName,
                     CONVERT(varchar(10), l.FromDate, 105) AS FromDate,
                     CONVERT(varchar(10), l.ToDate, 105) AS ToDate,
                     l.LeaveType, l.Status
              FROM dbo.LeaveRequest l INNER JOIN dbo.Staff s ON s.StaffId = l.StaffId
              WHERE s.BranchId = @branchId ORDER BY l.LeaveId DESC", new { branchId }, cancellationToken: ct));
        return rows.ToList();
    }
}

public sealed class PayrollRepository : IPayrollRepository
{
    private readonly IDbConnectionFactory _f;
    public PayrollRepository(IDbConnectionFactory f) => _f = f;

    public async Task<long?> GetStaffIdByCodeAsync(int branchId, string code, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        return await c.QuerySingleOrDefaultAsync<long?>(new CommandDefinition(
            "SELECT StaffId FROM dbo.Staff WHERE BranchId = @branchId AND EmployeeCode = @code AND IsActive = 1",
            new { branchId, code }, cancellationToken: ct));
    }

    public async Task<long> UpsertRunAsync(PayrollRun r, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        var existing = await c.QuerySingleOrDefaultAsync<long?>(new CommandDefinition(
            "SELECT PayrollId FROM dbo.PayrollRun WHERE StaffId = @StaffId AND PeriodYear = @PeriodYear AND PeriodMonth = @PeriodMonth",
            r, cancellationToken: ct));
        if (existing is long id)
        {
            await c.ExecuteAsync(new CommandDefinition(
                @"UPDATE dbo.PayrollRun SET BasicPay = @BasicPay, OvertimeHours = @OvertimeHours, OvertimeAmount = @OvertimeAmount,
                         GrossPay = @GrossPay, NetPay = @NetPay, Status = 'Draft', OvertimeApprovedBy = NULL
                  WHERE PayrollId = @id",
                new { r.BasicPay, r.OvertimeHours, r.OvertimeAmount, r.GrossPay, r.NetPay, id }, cancellationToken: ct));
            return id;
        }
        const string sql = @"
INSERT INTO dbo.PayrollRun (StaffId, PeriodYear, PeriodMonth, BasicPay, OvertimeHours, OvertimeAmount, GrossPay, NetPay, Status)
VALUES (@StaffId, @PeriodYear, @PeriodMonth, @BasicPay, @OvertimeHours, @OvertimeAmount, @GrossPay, @NetPay, @Status);
SELECT CAST(SCOPE_IDENTITY() AS BIGINT);";
        return await c.QuerySingleAsync<long>(new CommandDefinition(sql, r, cancellationToken: ct));
    }

    public async Task<PayrollRun?> GetRunAsync(long payrollId, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        return await c.QuerySingleOrDefaultAsync<PayrollRun>(new CommandDefinition(
            "SELECT * FROM dbo.PayrollRun WHERE PayrollId = @payrollId", new { payrollId }, cancellationToken: ct));
    }

    public async Task ApproveOvertimeAsync(long payrollId, long approvedBy, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        await c.ExecuteAsync(new CommandDefinition(
            "UPDATE dbo.PayrollRun SET OvertimeApprovedBy = @approvedBy, Status = 'Approved' WHERE PayrollId = @payrollId",
            new { payrollId, approvedBy }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<(long, string, string, decimal, decimal, decimal, decimal, string)>> GetRunsAsync(int branchId, int year, int month, CancellationToken ct = default)
    {
        using var c = await _f.CreateOpenConnectionAsync(ct);
        var rows = await c.QueryAsync<(long, string, string, decimal, decimal, decimal, decimal, string)>(new CommandDefinition(
            @"SELECT p.PayrollId, s.EmployeeCode, s.FullName, p.BasicPay, p.OvertimeHours, p.OvertimeAmount, p.NetPay, p.Status
              FROM dbo.PayrollRun p INNER JOIN dbo.Staff s ON s.StaffId = p.StaffId
              WHERE s.BranchId = @branchId AND p.PeriodYear = @year AND p.PeriodMonth = @month
              ORDER BY s.FullName", new { branchId, year, month }, cancellationToken: ct));
        return rows.ToList();
    }
}
