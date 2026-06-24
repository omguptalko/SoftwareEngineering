using FluentValidation;
using HIS.Application.Abstractions;
using HIS.Domain.Entities;
using HIS.Shared.Context;

namespace HIS.Application.Features.Hr;

// ============================ Staff master (SRS §3.17) ============================
public sealed record AddStaffCommand(string EmployeeCode, string FullName, string? Designation, string? Department, DateTime? DateOfJoining)
    : ICommand<long>, IAuditable
{
    public string AuditEntity => "Staff";
    public string? AuditEntityId => EmployeeCode;
}

public sealed class AddStaffValidator : AbstractValidator<AddStaffCommand>
{
    public AddStaffValidator()
    {
        RuleFor(x => x.EmployeeCode).NotEmpty();
        RuleFor(x => x.FullName).NotEmpty();
    }
}

public sealed class AddStaffHandler : MediatR.IRequestHandler<AddStaffCommand, long>
{
    private readonly IHrRepository _hr;
    private readonly IBranchContext _ctx;
    public AddStaffHandler(IHrRepository hr, IBranchContext ctx) { _hr = hr; _ctx = ctx; }

    public async Task<long> Handle(AddStaffCommand c, CancellationToken ct)
    {
        var branchId = _ctx.BranchId ?? throw new InvalidOperationException("Branch context required.");
        if (await _hr.GetStaffIdByCodeAsync(branchId, c.EmployeeCode, ct) is not null)
            throw new InvalidOperationException($"Employee code '{c.EmployeeCode}' already exists.");
        return await _hr.InsertStaffAsync(new Staff
        {
            BranchId = branchId, EmployeeCode = c.EmployeeCode, FullName = c.FullName,
            Designation = c.Designation, Department = c.Department, DateOfJoining = c.DateOfJoining, IsActive = true
        }, ct);
    }
}

public sealed record StaffDto(long StaffId, string EmployeeCode, string FullName, string? Designation, string? Department);
public sealed record GetStaffQuery : IQuery<IReadOnlyList<StaffDto>>;

public sealed class GetStaffHandler : MediatR.IRequestHandler<GetStaffQuery, IReadOnlyList<StaffDto>>
{
    private readonly IHrRepository _hr;
    private readonly IBranchContext _ctx;
    public GetStaffHandler(IHrRepository hr, IBranchContext ctx) { _hr = hr; _ctx = ctx; }

    public async Task<IReadOnlyList<StaffDto>> Handle(GetStaffQuery q, CancellationToken ct)
    {
        var rows = await _hr.GetStaffAsync(_ctx.BranchId ?? 0, ct);
        return rows.Select(s => new StaffDto(s.StaffId, s.EmployeeCode, s.FullName, s.Designation, s.Department)).ToList();
    }
}

// ============================ Attendance (SRS §3.17) ============================
public sealed record MarkAttendanceCommand(string EmployeeCode, DateTime WorkDate, string Status, string? InTime, string? OutTime)
    : ICommand<bool>, IAuditable
{
    public string AuditEntity => "Attendance";
    public string? AuditEntityId => EmployeeCode;
}

public sealed class MarkAttendanceValidator : AbstractValidator<MarkAttendanceCommand>
{
    public MarkAttendanceValidator()
    {
        RuleFor(x => x.EmployeeCode).NotEmpty();
        RuleFor(x => x.Status).NotEmpty();
    }
}

public sealed class MarkAttendanceHandler : MediatR.IRequestHandler<MarkAttendanceCommand, bool>
{
    private readonly IHrRepository _hr;
    private readonly IBranchContext _ctx;
    public MarkAttendanceHandler(IHrRepository hr, IBranchContext ctx) { _hr = hr; _ctx = ctx; }

    public async Task<bool> Handle(MarkAttendanceCommand c, CancellationToken ct)
    {
        var branchId = _ctx.BranchId ?? throw new InvalidOperationException("Branch context required.");
        var staffId = await _hr.GetStaffIdByCodeAsync(branchId, c.EmployeeCode, ct)
            ?? throw new InvalidOperationException($"Unknown employee '{c.EmployeeCode}'.");
        await _hr.UpsertAttendanceAsync(new Attendance
        {
            StaffId = staffId, WorkDate = c.WorkDate.Date, Status = c.Status,
            InTime = TimeSpan.TryParse(c.InTime, out var i) ? i : null,
            OutTime = TimeSpan.TryParse(c.OutTime, out var o) ? o : null
        }, ct);
        return true;
    }
}

public sealed record AttendanceRowDto(string EmployeeCode, string Name, string Status, string? InTime, string? OutTime);
public sealed record GetAttendanceQuery(DateTime Date) : IQuery<IReadOnlyList<AttendanceRowDto>>;

public sealed class GetAttendanceHandler : MediatR.IRequestHandler<GetAttendanceQuery, IReadOnlyList<AttendanceRowDto>>
{
    private readonly IHrRepository _hr;
    private readonly IBranchContext _ctx;
    public GetAttendanceHandler(IHrRepository hr, IBranchContext ctx) { _hr = hr; _ctx = ctx; }

    public async Task<IReadOnlyList<AttendanceRowDto>> Handle(GetAttendanceQuery q, CancellationToken ct)
    {
        var rows = await _hr.GetAttendanceAsync(_ctx.BranchId ?? 0, q.Date.Date, ct);
        return rows.Select(r => new AttendanceRowDto(r.EmployeeCode, r.Name, r.Status, r.InTime, r.OutTime)).ToList();
    }
}

// ============================ Leave (SRS §3.17) ============================
public sealed record RequestLeaveCommand(string EmployeeCode, DateTime FromDate, DateTime ToDate, string? LeaveType)
    : ICommand<long>, IAuditable
{
    public string AuditEntity => "LeaveRequest";
    public string? AuditEntityId => EmployeeCode;
}

public sealed class RequestLeaveValidator : AbstractValidator<RequestLeaveCommand>
{
    public RequestLeaveValidator()
    {
        RuleFor(x => x.EmployeeCode).NotEmpty();
        RuleFor(x => x.ToDate).GreaterThanOrEqualTo(x => x.FromDate).WithMessage("To date must be on/after From date.");
    }
}

public sealed class RequestLeaveHandler : MediatR.IRequestHandler<RequestLeaveCommand, long>
{
    private readonly IHrRepository _hr;
    private readonly IBranchContext _ctx;
    public RequestLeaveHandler(IHrRepository hr, IBranchContext ctx) { _hr = hr; _ctx = ctx; }

    public async Task<long> Handle(RequestLeaveCommand c, CancellationToken ct)
    {
        var branchId = _ctx.BranchId ?? throw new InvalidOperationException("Branch context required.");
        var staffId = await _hr.GetStaffIdByCodeAsync(branchId, c.EmployeeCode, ct)
            ?? throw new InvalidOperationException($"Unknown employee '{c.EmployeeCode}'.");
        return await _hr.InsertLeaveAsync(new LeaveRequest
        {
            StaffId = staffId, FromDate = c.FromDate.Date, ToDate = c.ToDate.Date, LeaveType = c.LeaveType, Status = "Pending"
        }, ct);
    }
}

public sealed record SetLeaveStatusCommand(long LeaveId, string Status) : ICommand<bool>, IAuditable
{
    public string AuditEntity => "LeaveRequest";
    public string? AuditEntityId => LeaveId.ToString();
}

public sealed class SetLeaveStatusHandler : MediatR.IRequestHandler<SetLeaveStatusCommand, bool>
{
    private readonly IHrRepository _hr;
    public SetLeaveStatusHandler(IHrRepository hr) { _hr = hr; }

    public async Task<bool> Handle(SetLeaveStatusCommand c, CancellationToken ct)
    {
        await _hr.SetLeaveStatusAsync(c.LeaveId, c.Status, ct);
        return true;
    }
}

public sealed record LeaveRowDto(long LeaveId, string Name, string FromDate, string ToDate, string? Type, string Status);
public sealed record GetLeavesQuery : IQuery<IReadOnlyList<LeaveRowDto>>;

public sealed class GetLeavesHandler : MediatR.IRequestHandler<GetLeavesQuery, IReadOnlyList<LeaveRowDto>>
{
    private readonly IHrRepository _hr;
    private readonly IBranchContext _ctx;
    public GetLeavesHandler(IHrRepository hr, IBranchContext ctx) { _hr = hr; _ctx = ctx; }

    public async Task<IReadOnlyList<LeaveRowDto>> Handle(GetLeavesQuery q, CancellationToken ct)
    {
        var rows = await _hr.GetLeavesAsync(_ctx.BranchId ?? 0, ct);
        return rows.Select(r => new LeaveRowDto(r.LeaveId, r.Name, r.FromDate, r.ToDate, r.Type, r.Status)).ToList();
    }
}
