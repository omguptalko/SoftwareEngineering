using FluentValidation;
using HIS.Application.Abstractions;
using HIS.Domain.Entities;

namespace HIS.Application.Features.Masters;

// ============================ Doctor master admin (doctor list) ============================
public sealed record DoctorDto(int DoctorId, string Code, string Name, string Department, decimal? ConsultationFee, bool IsActive);

public sealed record GetDoctorsAdminQuery : IQuery<IReadOnlyList<DoctorDto>>, IRequireAuthentication;

public sealed class GetDoctorsAdminHandler : MediatR.IRequestHandler<GetDoctorsAdminQuery, IReadOnlyList<DoctorDto>>
{
    private readonly ILookupRepository _lk;
    public GetDoctorsAdminHandler(ILookupRepository lk) => _lk = lk;

    public async Task<IReadOnlyList<DoctorDto>> Handle(GetDoctorsAdminQuery q, CancellationToken ct)
    {
        var rows = await _lk.GetAllDoctorsAsync(ct);
        return rows.Select(d => new DoctorDto(d.DoctorId, d.Code, d.Name, d.Department, d.ConsultationFee, d.IsActive)).ToList();
    }
}

// ============================ Add / update a doctor ============================
/// <summary>Create (DoctorId null/0) or update a doctor in the master. Code is immutable on update.</summary>
public sealed record SaveDoctorCommand(int? DoctorId, string Code, string Name, string Department, decimal? ConsultationFee)
    : ICommand<int>, IAuditable, IAuthorizable
{
    public string AuditEntity => "Doctor";
    public string? AuditEntityId => Code;
    public string RequiredPermission => "masters.manage";
}

public sealed class SaveDoctorValidator : AbstractValidator<SaveDoctorCommand>
{
    public SaveDoctorValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(20).Matches("^[A-Za-z0-9-]+$")
            .WithMessage("Code may use letters, digits and - only.");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Department).NotEmpty().MaximumLength(80);
        RuleFor(x => x.ConsultationFee).GreaterThanOrEqualTo(0).When(x => x.ConsultationFee.HasValue)
            .WithMessage("Consultation fee cannot be negative.");
    }
}

public sealed class SaveDoctorHandler : MediatR.IRequestHandler<SaveDoctorCommand, int>
{
    private readonly ILookupRepository _lk;
    public SaveDoctorHandler(ILookupRepository lk) => _lk = lk;

    public async Task<int> Handle(SaveDoctorCommand c, CancellationToken ct)
    {
        var isNew = c.DoctorId is null or 0;
        if (await _lk.DoctorCodeExistsAsync(c.Code, isNew ? null : c.DoctorId, ct))
            throw new InvalidOperationException($"A doctor with code '{c.Code}' already exists.");

        if (isNew)
            return await _lk.InsertDoctorAsync(new Doctor
            {
                Code = c.Code, Name = c.Name, Department = c.Department, ConsultationFee = c.ConsultationFee, IsActive = true
            }, ct);

        await _lk.UpdateDoctorAsync(new Doctor { DoctorId = c.DoctorId!.Value, Name = c.Name, Department = c.Department, ConsultationFee = c.ConsultationFee }, ct);
        return c.DoctorId.Value;
    }
}

// ============================ Activate / deactivate a doctor ============================
public sealed record SetDoctorActiveCommand(int DoctorId, bool IsActive) : ICommand<bool>, IAuditable, IAuthorizable
{
    public string AuditEntity => "Doctor";
    public string? AuditEntityId => DoctorId.ToString();
    public string RequiredPermission => "masters.manage";
}

public sealed class SetDoctorActiveHandler : MediatR.IRequestHandler<SetDoctorActiveCommand, bool>
{
    private readonly ILookupRepository _lk;
    public SetDoctorActiveHandler(ILookupRepository lk) => _lk = lk;

    public async Task<bool> Handle(SetDoctorActiveCommand c, CancellationToken ct)
    {
        await _lk.SetDoctorActiveAsync(c.DoctorId, c.IsActive, ct);
        return true;
    }
}
