using FluentValidation;
using HIS.Application.Abstractions;
using HIS.Application.Features.Appointments;   // LookupCode
using HIS.Domain.Entities;
using HIS.Shared.Context;

namespace HIS.Application.Features.OccHealth;

// ============================ Company contracts (SRS §3.23) ============================
public sealed record AddCompanyContractCommand(string CompanyName, string? PayerCode, string? ContractType, DateTime? ValidFrom, DateTime? ValidTo)
    : ICommand<int>, IAuditable
{
    public string AuditEntity => "CompanyContract";
    public string? AuditEntityId => CompanyName;
}

public sealed class AddCompanyContractValidator : AbstractValidator<AddCompanyContractCommand>
{
    public AddCompanyContractValidator() => RuleFor(x => x.CompanyName).NotEmpty();
}

public sealed class AddCompanyContractHandler : MediatR.IRequestHandler<AddCompanyContractCommand, int>
{
    private readonly IOccHealthRepository _occ;
    public AddCompanyContractHandler(IOccHealthRepository occ) { _occ = occ; }

    public async Task<int> Handle(AddCompanyContractCommand c, CancellationToken ct)
        => (int)await _occ.InsertContractAsync(new CompanyContract
        {
            CompanyName = c.CompanyName, PayerCode = c.PayerCode, ContractType = c.ContractType,
            ValidFrom = c.ValidFrom, ValidTo = c.ValidTo, IsActive = true
        }, ct);
}

public sealed record ContractDto(int ContractId, string CompanyName, string? ContractType);
public sealed record GetContractsQuery : IQuery<IReadOnlyList<ContractDto>>;

public sealed class GetContractsHandler : MediatR.IRequestHandler<GetContractsQuery, IReadOnlyList<ContractDto>>
{
    private readonly IOccHealthRepository _occ;
    public GetContractsHandler(IOccHealthRepository occ) { _occ = occ; }

    public async Task<IReadOnlyList<ContractDto>> Handle(GetContractsQuery q, CancellationToken ct)
        => (await _occ.GetContractsAsync(ct)).Select(c => new ContractDto(c.ContractId, c.CompanyName, c.ContractType)).ToList();
}

// ============================ Medical exam PEME/PME (SRS §3.23) ============================
public sealed record ConductMedicalExamCommand(
    string PatientUhid, int? ContractId, string ExamType, DateTime ExamDate, string? FitnessResult,
    string? Audiometry, string? Spirometry, string? Vision, string? VaccinationNotes)
    : ICommand<long>, IAuditable
{
    public string AuditEntity => "MedicalExam";
    public string? AuditEntityId => PatientUhid;
}

public sealed class ConductMedicalExamValidator : AbstractValidator<ConductMedicalExamCommand>
{
    public ConductMedicalExamValidator()
    {
        RuleFor(x => x.PatientUhid).NotEmpty();
        RuleFor(x => x.ExamType).NotEmpty().Must(e => e is "PEME" or "PME").WithMessage("ExamType must be PEME or PME.");
        RuleFor(x => x.FitnessResult).Must(f => f is null or "Fit" or "Unfit" or "Fit-with-conditions")
            .WithMessage("Invalid fitness result.");
    }
}

public sealed class ConductMedicalExamHandler : MediatR.IRequestHandler<ConductMedicalExamCommand, long>
{
    private readonly IOccHealthRepository _occ;
    private readonly IPatientRepository _patients;
    private readonly IBranchContext _ctx;
    public ConductMedicalExamHandler(IOccHealthRepository occ, IPatientRepository patients, IBranchContext ctx)
    { _occ = occ; _patients = patients; _ctx = ctx; }

    public async Task<long> Handle(ConductMedicalExamCommand c, CancellationToken ct)
    {
        var branchId = _ctx.BranchId ?? throw new InvalidOperationException("Branch context required.");
        var patient = await _patients.GetByUhidAsync(LookupCode.Parse(c.PatientUhid), ct)
            ?? throw new InvalidOperationException($"Unknown patient '{c.PatientUhid}'.");
        return await _occ.InsertExamAsync(new MedicalExam
        {
            BranchId = branchId, PatientId = patient.PatientId, ContractId = c.ContractId,
            ExamType = c.ExamType, ExamDate = c.ExamDate.Date, FitnessResult = c.FitnessResult,
            Audiometry = c.Audiometry, Spirometry = c.Spirometry, Vision = c.Vision, VaccinationNotes = c.VaccinationNotes
        }, ct);
    }
}

public sealed record MedicalExamRowDto(long ExamId, string Patient, string? Company, string ExamType, string ExamDate, string? Fitness);
public sealed record GetMedicalExamsQuery : IQuery<IReadOnlyList<MedicalExamRowDto>>;

public sealed class GetMedicalExamsHandler : MediatR.IRequestHandler<GetMedicalExamsQuery, IReadOnlyList<MedicalExamRowDto>>
{
    private readonly IOccHealthRepository _occ;
    private readonly IBranchContext _ctx;
    public GetMedicalExamsHandler(IOccHealthRepository occ, IBranchContext ctx) { _occ = occ; _ctx = ctx; }

    public async Task<IReadOnlyList<MedicalExamRowDto>> Handle(GetMedicalExamsQuery q, CancellationToken ct)
        => (await _occ.GetExamsAsync(_ctx.BranchId ?? 0, ct))
            .Select(e => new MedicalExamRowDto(e.ExamId, e.Patient, e.Company, e.ExamType, e.ExamDate, e.Fitness)).ToList();
}

// ============================ Hazard exposure + injury (SRS §3.23) ============================
public sealed record RecordHazardCommand(string PatientUhid, string HazardType, DateTime RecordedDate, string? Notes)
    : ICommand<long>, IAuditable
{
    public string AuditEntity => "HazardExposure";
    public string? AuditEntityId => PatientUhid;
}

public sealed class RecordHazardValidator : AbstractValidator<RecordHazardCommand>
{
    public RecordHazardValidator()
    {
        RuleFor(x => x.PatientUhid).NotEmpty();
        RuleFor(x => x.HazardType).NotEmpty();
    }
}

public sealed class RecordHazardHandler : MediatR.IRequestHandler<RecordHazardCommand, long>
{
    private readonly IOccHealthRepository _occ;
    private readonly IPatientRepository _patients;
    public RecordHazardHandler(IOccHealthRepository occ, IPatientRepository patients) { _occ = occ; _patients = patients; }

    public async Task<long> Handle(RecordHazardCommand c, CancellationToken ct)
    {
        var patient = await _patients.GetByUhidAsync(LookupCode.Parse(c.PatientUhid), ct)
            ?? throw new InvalidOperationException($"Unknown patient '{c.PatientUhid}'.");
        return await _occ.InsertHazardAsync(new HazardExposure
        {
            PatientId = patient.PatientId, HazardType = c.HazardType, RecordedDate = c.RecordedDate.Date, Notes = c.Notes
        }, ct);
    }
}

public sealed record RecordInjuryCommand(string PatientUhid, int? ContractId, DateTime InjuryDate, bool MlcLinked, string? Description)
    : ICommand<long>, IAuditable
{
    public string AuditEntity => "WorkplaceInjury";
    public string? AuditEntityId => PatientUhid;
}

public sealed class RecordInjuryValidator : AbstractValidator<RecordInjuryCommand>
{
    public RecordInjuryValidator() => RuleFor(x => x.PatientUhid).NotEmpty();
}

public sealed class RecordInjuryHandler : MediatR.IRequestHandler<RecordInjuryCommand, long>
{
    private readonly IOccHealthRepository _occ;
    private readonly IPatientRepository _patients;
    public RecordInjuryHandler(IOccHealthRepository occ, IPatientRepository patients) { _occ = occ; _patients = patients; }

    public async Task<long> Handle(RecordInjuryCommand c, CancellationToken ct)
    {
        var patient = await _patients.GetByUhidAsync(LookupCode.Parse(c.PatientUhid), ct)
            ?? throw new InvalidOperationException($"Unknown patient '{c.PatientUhid}'.");
        return await _occ.InsertInjuryAsync(new WorkplaceInjury
        {
            PatientId = patient.PatientId, ContractId = c.ContractId, InjuryDate = c.InjuryDate,
            MlcLinked = c.MlcLinked, Description = c.Description
        }, ct);
    }
}

public sealed record InjuryRowDto(long InjuryId, string Patient, string InjuryDate, bool MlcLinked, string? Description);
public sealed record GetInjuriesQuery : IQuery<IReadOnlyList<InjuryRowDto>>;

public sealed class GetInjuriesHandler : MediatR.IRequestHandler<GetInjuriesQuery, IReadOnlyList<InjuryRowDto>>
{
    private readonly IOccHealthRepository _occ;
    private readonly IBranchContext _ctx;
    public GetInjuriesHandler(IOccHealthRepository occ, IBranchContext ctx) { _occ = occ; _ctx = ctx; }

    public async Task<IReadOnlyList<InjuryRowDto>> Handle(GetInjuriesQuery q, CancellationToken ct)
        => (await _occ.GetInjuriesAsync(_ctx.BranchId ?? 0, ct))
            .Select(i => new InjuryRowDto(i.InjuryId, i.Patient, i.InjuryDate, i.MlcLinked, i.Description)).ToList();
}
