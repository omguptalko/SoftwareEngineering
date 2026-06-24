using FluentValidation;
using HIS.Application.Abstractions;
using HIS.Shared.Context;

namespace HIS.Application.Features.Tenant;

// ============================ Resolved context ============================
public sealed record TenantContextDto(
    bool IsResolved, int? TenantId, string? TenantCode, string? FiscalYearCode, string? MasterDb, string? DataDb);

public sealed record GetTenantContextQuery : IQuery<TenantContextDto>, IRequireAuthentication;

public sealed class GetTenantContextHandler : MediatR.IRequestHandler<GetTenantContextQuery, TenantContextDto>
{
    private readonly ITenantContext _t;
    public GetTenantContextHandler(ITenantContext t) => _t = t;
    public Task<TenantContextDto> Handle(GetTenantContextQuery q, CancellationToken ct) =>
        Task.FromResult(new TenantContextDto(_t.IsResolved, _t.TenantId, _t.TenantCode, _t.FiscalYearCode, _t.MasterDb, _t.DataDb));
}

// ============================ Patients (tenant MASTER DB) ============================
public sealed record AddTenantPatientCommand(string FullName, string? Mobile) : ICommand<AddTenantPatientResult>, IRequireAuthentication;
public sealed record AddTenantPatientResult(long PatientId, string Uhid);
public sealed class AddTenantPatientValidator : AbstractValidator<AddTenantPatientCommand>
{
    public AddTenantPatientValidator() => RuleFor(x => x.FullName).NotEmpty().MaximumLength(160);
}
public sealed class AddTenantPatientHandler : MediatR.IRequestHandler<AddTenantPatientCommand, AddTenantPatientResult>
{
    private readonly ITenantScopedRepository _repo;
    public AddTenantPatientHandler(ITenantScopedRepository repo) => _repo = repo;
    public async Task<AddTenantPatientResult> Handle(AddTenantPatientCommand c, CancellationToken ct)
    {
        var (id, uhid) = await _repo.AddPatientAsync(c.FullName, c.Mobile, ct);
        return new AddTenantPatientResult(id, uhid);
    }
}

public sealed record TenantPatientRow(long PatientId, string Uhid, string FullName);
public sealed record GetTenantPatientsQuery : IQuery<IReadOnlyList<TenantPatientRow>>, IRequireAuthentication;
public sealed class GetTenantPatientsHandler : MediatR.IRequestHandler<GetTenantPatientsQuery, IReadOnlyList<TenantPatientRow>>
{
    private readonly ITenantScopedRepository _repo;
    public GetTenantPatientsHandler(ITenantScopedRepository repo) => _repo = repo;
    public async Task<IReadOnlyList<TenantPatientRow>> Handle(GetTenantPatientsQuery q, CancellationToken ct)
        => (await _repo.GetPatientsAsync(ct)).Select(r => new TenantPatientRow(r.Item1, r.Item2, r.Item3)).ToList();
}

// ============================ Bills (tenant current-FY DATA DB) ============================
public sealed record AddTenantBillCommand(decimal Gross) : ICommand<AddTenantBillResult>, IRequireAuthentication;
public sealed record AddTenantBillResult(long BillId, string BillNo);
public sealed class AddTenantBillValidator : AbstractValidator<AddTenantBillCommand>
{
    public AddTenantBillValidator() => RuleFor(x => x.Gross).GreaterThan(0);
}
public sealed class AddTenantBillHandler : MediatR.IRequestHandler<AddTenantBillCommand, AddTenantBillResult>
{
    private readonly ITenantScopedRepository _repo;
    public AddTenantBillHandler(ITenantScopedRepository repo) => _repo = repo;
    public async Task<AddTenantBillResult> Handle(AddTenantBillCommand c, CancellationToken ct)
    {
        var (id, no) = await _repo.AddBillAsync(c.Gross, ct);
        return new AddTenantBillResult(id, no);
    }
}

public sealed record TenantBillRow(long BillId, string BillNo, decimal Gross, string Status);
public sealed record GetTenantBillsQuery : IQuery<IReadOnlyList<TenantBillRow>>, IRequireAuthentication;
public sealed class GetTenantBillsHandler : MediatR.IRequestHandler<GetTenantBillsQuery, IReadOnlyList<TenantBillRow>>
{
    private readonly ITenantScopedRepository _repo;
    public GetTenantBillsHandler(ITenantScopedRepository repo) => _repo = repo;
    public async Task<IReadOnlyList<TenantBillRow>> Handle(GetTenantBillsQuery q, CancellationToken ct)
        => (await _repo.GetBillsAsync(ct)).Select(r => new TenantBillRow(r.Item1, r.Item2, r.Item3, r.Item4)).ToList();
}
