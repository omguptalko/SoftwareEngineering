using FluentValidation;
using HIS.Application.Abstractions;
using HIS.Domain.Entities;
using HIS.Shared.Context;
using Microsoft.Extensions.Configuration;

namespace HIS.Application.Features.Hr;

// ============================ Run payroll (SRS §3.18) ============================
public sealed record RunPayrollCommand(string EmployeeCode, int Year, int Month, decimal BasicPay, decimal OvertimeHours)
    : ICommand<RunPayrollResult>, IAuditable
{
    public string AuditEntity => "PayrollRun";
    public string? AuditEntityId => $"{EmployeeCode}:{Year}-{Month}";
}
public sealed record RunPayrollResult(long PayrollId, decimal OvertimeAmount, decimal GrossPay, decimal NetPay);

public sealed class RunPayrollValidator : AbstractValidator<RunPayrollCommand>
{
    public RunPayrollValidator()
    {
        RuleFor(x => x.EmployeeCode).NotEmpty();
        RuleFor(x => x.Year).InclusiveBetween(2000, 2100);
        RuleFor(x => x.Month).InclusiveBetween(1, 12);
        RuleFor(x => x.BasicPay).GreaterThanOrEqualTo(0);
        RuleFor(x => x.OvertimeHours).GreaterThanOrEqualTo(0);
    }
}

public sealed class RunPayrollHandler : MediatR.IRequestHandler<RunPayrollCommand, RunPayrollResult>
{
    private readonly IPayrollRepository _payroll;
    private readonly IBranchContext _ctx;
    private readonly IConfiguration _config;

    public RunPayrollHandler(IPayrollRepository payroll, IBranchContext ctx, IConfiguration config)
    { _payroll = payroll; _ctx = ctx; _config = config; }

    public async Task<RunPayrollResult> Handle(RunPayrollCommand c, CancellationToken ct)
    {
        var branchId = _ctx.BranchId ?? throw new InvalidOperationException("Branch context required.");
        var staffId = await _payroll.GetStaffIdByCodeAsync(branchId, c.EmployeeCode, ct)
            ?? throw new InvalidOperationException($"Unknown employee '{c.EmployeeCode}'.");

        // OT rate + PF deduction are config-driven, never hardcoded.
        var otRate = _config.GetValue("Payroll:OvertimeRatePerHour", 150m);
        var pfPct = _config.GetValue("Payroll:PfDeductionPct", 12m);

        var otAmount = Math.Round(c.OvertimeHours * otRate, 2);
        var gross = c.BasicPay + otAmount;
        var net = Math.Round(gross - (c.BasicPay * pfPct / 100m), 2);

        var id = await _payroll.UpsertRunAsync(new PayrollRun
        {
            StaffId = staffId, PeriodYear = c.Year, PeriodMonth = c.Month,
            BasicPay = c.BasicPay, OvertimeHours = c.OvertimeHours, OvertimeAmount = otAmount,
            GrossPay = gross, NetPay = net, Status = "Draft"
        }, ct);

        return new RunPayrollResult(id, otAmount, gross, net);
    }
}

// ============================ Approve overtime (SRS §3.18) ============================
public sealed record ApproveOvertimeCommand(long PayrollId, long ApprovedBy) : ICommand<bool>, IAuditable
{
    public string AuditEntity => "PayrollRun";
    public string? AuditEntityId => PayrollId.ToString();
}

public sealed class ApproveOvertimeValidator : AbstractValidator<ApproveOvertimeCommand>
{
    public ApproveOvertimeValidator()
    {
        RuleFor(x => x.PayrollId).GreaterThan(0);
        RuleFor(x => x.ApprovedBy).GreaterThan(0);
    }
}

public sealed class ApproveOvertimeHandler : MediatR.IRequestHandler<ApproveOvertimeCommand, bool>
{
    private readonly IPayrollRepository _payroll;
    public ApproveOvertimeHandler(IPayrollRepository payroll) { _payroll = payroll; }

    public async Task<bool> Handle(ApproveOvertimeCommand c, CancellationToken ct)
    {
        var run = await _payroll.GetRunAsync(c.PayrollId, ct) ?? throw new InvalidOperationException("Payroll run not found.");
        if (run.Status == "Approved") throw new InvalidOperationException("Already approved.");
        await _payroll.ApproveOvertimeAsync(c.PayrollId, c.ApprovedBy, ct);
        return true;
    }
}

// ============================ Payroll + OT summary (SRS §3.18) ============================
public sealed record PayrollRowDto(long PayrollId, string EmployeeCode, string Name, decimal Basic, decimal OtHours, decimal OtAmount, decimal Net, string Status);
public sealed record PayrollSummaryDto(int Year, int Month, IReadOnlyList<PayrollRowDto> Rows, decimal TotalOtHours, decimal TotalOtAmount, decimal TotalNet);

public sealed record GetPayrollQuery(int Year, int Month) : IQuery<PayrollSummaryDto>;

public sealed class GetPayrollHandler : MediatR.IRequestHandler<GetPayrollQuery, PayrollSummaryDto>
{
    private readonly IPayrollRepository _payroll;
    private readonly IBranchContext _ctx;
    public GetPayrollHandler(IPayrollRepository payroll, IBranchContext ctx) { _payroll = payroll; _ctx = ctx; }

    public async Task<PayrollSummaryDto> Handle(GetPayrollQuery q, CancellationToken ct)
    {
        var rows = await _payroll.GetRunsAsync(_ctx.BranchId ?? 0, q.Year, q.Month, ct);
        var dtos = rows.Select(r => new PayrollRowDto(r.PayrollId, r.EmployeeCode, r.Name, r.Basic, r.OtHours, r.OtAmount, r.Net, r.Status)).ToList();
        return new PayrollSummaryDto(q.Year, q.Month, dtos, dtos.Sum(d => d.OtHours), dtos.Sum(d => d.OtAmount), dtos.Sum(d => d.Net));
    }
}
