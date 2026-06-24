using FluentValidation;
using HIS.Application.Abstractions;
using HIS.Application.Features.Appointments;   // LookupCode
using HIS.Domain.Entities;
using HIS.Shared.Context;

namespace HIS.Application.Features.Diagnostics;

// ---- Create lab order (SRS §3.8) ----
public sealed record CreateLabOrderCommand(string PatientUhid, string TestName)
    : ICommand<CreateLabOrderResult>, IAuditable
{
    public string AuditEntity => "LabOrder";
    public string? AuditEntityId => PatientUhid;
}
public sealed record CreateLabOrderResult(long LabOrderId, string Barcode);

public sealed class CreateLabOrderValidator : AbstractValidator<CreateLabOrderCommand>
{
    public CreateLabOrderValidator()
    {
        RuleFor(x => x.PatientUhid).NotEmpty();
        RuleFor(x => x.TestName).NotEmpty();
    }
}

public sealed class CreateLabOrderHandler : MediatR.IRequestHandler<CreateLabOrderCommand, CreateLabOrderResult>
{
    private readonly ILisRepository _lis;
    private readonly IPatientRepository _patients;
    private readonly IBranchContext _ctx;
    public CreateLabOrderHandler(ILisRepository lis, IPatientRepository patients, IBranchContext ctx)
    { _lis = lis; _patients = patients; _ctx = ctx; }

    public async Task<CreateLabOrderResult> Handle(CreateLabOrderCommand c, CancellationToken ct)
    {
        var branchId = _ctx.BranchId ?? throw new InvalidOperationException("Branch context required.");
        var patient = await _patients.GetByUhidAsync(LookupCode.Parse(c.PatientUhid), ct)
            ?? throw new InvalidOperationException($"Unknown patient '{c.PatientUhid}'.");

        var barcode = await _lis.NextBarcodeAsync(branchId, ct);
        var id = await _lis.CreateOrderAsync(new LabOrder
        {
            Barcode = barcode, PatientId = patient.PatientId, TestName = c.TestName,
            CollectedUtc = DateTime.UtcNow, Status = "Received"
        }, ct);
        return new CreateLabOrderResult(id, barcode);
    }
}

// ---- Worklist ----
public sealed record LabWorklistItemDto(long LabOrderId, string Barcode, string Patient, string Test, string Status);
public sealed record GetLabWorklistQuery : IQuery<IReadOnlyList<LabWorklistItemDto>>;

public sealed class GetLabWorklistHandler : MediatR.IRequestHandler<GetLabWorklistQuery, IReadOnlyList<LabWorklistItemDto>>
{
    private readonly ILisRepository _lis;
    private readonly IBranchContext _ctx;
    public GetLabWorklistHandler(ILisRepository lis, IBranchContext ctx) { _lis = lis; _ctx = ctx; }

    public async Task<IReadOnlyList<LabWorklistItemDto>> Handle(GetLabWorklistQuery q, CancellationToken ct)
    {
        var rows = await _lis.GetWorklistAsync(_ctx.BranchId ?? 0, ct);
        return rows.Select(r => new LabWorklistItemDto(r.LabOrderId, r.Barcode, r.Patient, r.Test, r.Status)).ToList();
    }
}

// ---- Enter & release results ----
public sealed record LabResultLineDto(string Parameter, string? ResultValue, string? Unit, string? ReferenceRange, string? Flag);

public sealed record EnterLabResultsCommand(long LabOrderId, IReadOnlyList<LabResultLineDto> Results)
    : ICommand<bool>, IAuditable
{
    public string AuditEntity => "LabResult";
    public string? AuditEntityId => LabOrderId.ToString();
}

public sealed class EnterLabResultsValidator : AbstractValidator<EnterLabResultsCommand>
{
    public EnterLabResultsValidator()
    {
        RuleFor(x => x.LabOrderId).GreaterThan(0);
        RuleFor(x => x.Results).NotEmpty().WithMessage("At least one result line is required.");
    }
}

public sealed class EnterLabResultsHandler : MediatR.IRequestHandler<EnterLabResultsCommand, bool>
{
    private readonly ILisRepository _lis;
    public EnterLabResultsHandler(ILisRepository lis) { _lis = lis; }

    public async Task<bool> Handle(EnterLabResultsCommand c, CancellationToken ct)
    {
        foreach (var r in c.Results.Where(x => !string.IsNullOrWhiteSpace(x.Parameter)))
            await _lis.AddResultAsync(new LabResult
            {
                LabOrderId = c.LabOrderId, Parameter = r.Parameter, ResultValue = r.ResultValue,
                Unit = r.Unit, ReferenceRange = r.ReferenceRange, Flag = r.Flag, ValidatedUtc = DateTime.UtcNow
            }, ct);

        await _lis.SetOrderStatusAsync(c.LabOrderId, "Released", ct);
        return true;
    }
}
