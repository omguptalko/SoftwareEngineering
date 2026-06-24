using HIS.Application.Features.Hr;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace HIS.Api.Controllers;

[ApiController]
[Route("api/hr")]
public sealed class HrController : ControllerBase
{
    private readonly IMediator _mediator;
    public HrController(IMediator mediator) => _mediator = mediator;

    [HttpGet("staff")]
    public Task<IReadOnlyList<StaffDto>> Staff(CancellationToken ct) => _mediator.Send(new GetStaffQuery(), ct);

    [HttpPost("staff")]
    public Task<long> AddStaff([FromBody] AddStaffCommand cmd, CancellationToken ct) => _mediator.Send(cmd, ct);

    [HttpGet("attendance")]
    public Task<IReadOnlyList<AttendanceRowDto>> Attendance([FromQuery] DateTime date, CancellationToken ct) => _mediator.Send(new GetAttendanceQuery(date), ct);

    [HttpPost("attendance")]
    public Task<bool> MarkAttendance([FromBody] MarkAttendanceCommand cmd, CancellationToken ct) => _mediator.Send(cmd, ct);

    [HttpGet("leaves")]
    public Task<IReadOnlyList<LeaveRowDto>> Leaves(CancellationToken ct) => _mediator.Send(new GetLeavesQuery(), ct);

    [HttpPost("leaves")]
    public Task<long> RequestLeave([FromBody] RequestLeaveCommand cmd, CancellationToken ct) => _mediator.Send(cmd, ct);

    [HttpPost("leaves/{leaveId:long}/status")]
    public Task<bool> SetLeaveStatus(long leaveId, [FromBody] LeaveStatusBody body, CancellationToken ct)
        => _mediator.Send(new SetLeaveStatusCommand(leaveId, body.Status), ct);

    public sealed record LeaveStatusBody(string Status);
}

[ApiController]
[Route("api/payroll")]
public sealed class PayrollController : ControllerBase
{
    private readonly IMediator _mediator;
    public PayrollController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public Task<PayrollSummaryDto> Get([FromQuery] int year, [FromQuery] int month, CancellationToken ct) => _mediator.Send(new GetPayrollQuery(year, month), ct);

    [HttpPost("run")]
    public Task<RunPayrollResult> Run([FromBody] RunPayrollCommand cmd, CancellationToken ct) => _mediator.Send(cmd, ct);

    [HttpPost("{payrollId:long}/approve")]
    public Task<bool> Approve(long payrollId, [FromBody] ApproveOvertimeBody body, CancellationToken ct)
        => _mediator.Send(new ApproveOvertimeCommand(payrollId, body.ApprovedBy), ct);

    public sealed record ApproveOvertimeBody(long ApprovedBy);
}
