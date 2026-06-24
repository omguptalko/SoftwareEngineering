using FluentValidation;
using HIS.Application.Abstractions;
using HIS.Application.Features.Appointments;   // LookupCode
using HIS.Domain.Entities;
using HIS.Shared.Context;

namespace HIS.Application.Features.Support;

// ============================ Diet (SRS §3.26) ============================
public sealed record OrderDietCommand(long AdmissionId, string DietType, decimal? Cost) : ICommand<long>, IAuditable
{
    public string AuditEntity => "DietOrder";
    public string? AuditEntityId => AdmissionId.ToString();
}
public sealed class OrderDietValidator : AbstractValidator<OrderDietCommand>
{
    public OrderDietValidator() { RuleFor(x => x.AdmissionId).GreaterThan(0); RuleFor(x => x.DietType).NotEmpty(); }
}
public sealed class OrderDietHandler : MediatR.IRequestHandler<OrderDietCommand, long>
{
    private readonly IStatutoryRepository _r;
    public OrderDietHandler(IStatutoryRepository r) { _r = r; }
    public Task<long> Handle(OrderDietCommand c, CancellationToken ct)
        => _r.InsertDietAsync(new DietOrder { AdmissionId = c.AdmissionId, DietType = c.DietType, OrderedUtc = DateTime.UtcNow, Cost = c.Cost }, ct);
}
public sealed record DietRowDto(long DietOrderId, string Patient, string DietType, decimal? Cost);
public sealed record GetDietQuery : IQuery<IReadOnlyList<DietRowDto>>;
public sealed class GetDietHandler : MediatR.IRequestHandler<GetDietQuery, IReadOnlyList<DietRowDto>>
{
    private readonly IStatutoryRepository _r; private readonly IBranchContext _ctx;
    public GetDietHandler(IStatutoryRepository r, IBranchContext ctx) { _r = r; _ctx = ctx; }
    public async Task<IReadOnlyList<DietRowDto>> Handle(GetDietQuery q, CancellationToken ct)
        => (await _r.GetDietAsync(_ctx.BranchId ?? 0, ct)).Select(d => new DietRowDto(d.DietOrderId, d.Patient, d.DietType, d.Cost)).ToList();
}

// ============================ BMWM (SRS §3.25) ============================
public sealed record GenerateWasteBagCommand(string Barcode, string ColourCode, decimal? WeightKg) : ICommand<long>, IAuditable
{
    public string AuditEntity => "WasteBag";
    public string? AuditEntityId => Barcode;
}
public sealed class GenerateWasteBagValidator : AbstractValidator<GenerateWasteBagCommand>
{
    public GenerateWasteBagValidator() { RuleFor(x => x.Barcode).NotEmpty(); RuleFor(x => x.ColourCode).NotEmpty(); }
}
public sealed class GenerateWasteBagHandler : MediatR.IRequestHandler<GenerateWasteBagCommand, long>
{
    private readonly IStatutoryRepository _r; private readonly IBranchContext _ctx;
    public GenerateWasteBagHandler(IStatutoryRepository r, IBranchContext ctx) { _r = r; _ctx = ctx; }
    public Task<long> Handle(GenerateWasteBagCommand c, CancellationToken ct)
        => _r.InsertWasteBagAsync(new WasteBag { BranchId = _ctx.BranchId ?? 0, Barcode = c.Barcode, ColourCode = c.ColourCode, WeightKg = c.WeightKg, GeneratedUtc = DateTime.UtcNow }, ct);
}
public sealed record HandoverWasteBagCommand(long BagId) : ICommand<bool>, IAuditable
{
    public string AuditEntity => "WasteBag";
    public string? AuditEntityId => BagId.ToString();
}
public sealed class HandoverWasteBagHandler : MediatR.IRequestHandler<HandoverWasteBagCommand, bool>
{
    private readonly IStatutoryRepository _r;
    public HandoverWasteBagHandler(IStatutoryRepository r) { _r = r; }
    public async Task<bool> Handle(HandoverWasteBagCommand c, CancellationToken ct) { await _r.HandoverWasteBagAsync(c.BagId, ct); return true; }
}
public sealed record WasteBagRowDto(string Barcode, string Colour, decimal? WeightKg, bool HandedOver);
public sealed record FormIvRowDto(string Colour, int Bags, decimal Weight);
public sealed record BmwmDto(IReadOnlyList<WasteBagRowDto> Bags, IReadOnlyList<FormIvRowDto> FormIv);
public sealed record GetBmwmQuery : IQuery<BmwmDto>;
public sealed class GetBmwmHandler : MediatR.IRequestHandler<GetBmwmQuery, BmwmDto>
{
    private readonly IStatutoryRepository _r; private readonly IBranchContext _ctx;
    public GetBmwmHandler(IStatutoryRepository r, IBranchContext ctx) { _r = r; _ctx = ctx; }
    public async Task<BmwmDto> Handle(GetBmwmQuery q, CancellationToken ct)
    {
        var b = _ctx.BranchId ?? 0;
        var bags = (await _r.GetWasteBagsAsync(b, ct)).Select(x => new WasteBagRowDto(x.Barcode, x.Colour, x.WeightKg, x.HandedOver)).ToList();
        var formiv = (await _r.GetFormIvAsync(b, ct)).Select(x => new FormIvRowDto(x.Colour, x.Bags, x.Weight)).ToList();
        return new BmwmDto(bags, formiv);
    }
}

// ============================ Mortuary (SRS §3.27) ============================
public sealed record AdmitBodyCommand(string? PatientUhid, string? StorageNo, bool MlcLinked, bool PoliceIntimated) : ICommand<long>, IAuditable
{
    public string AuditEntity => "MortuaryRecord";
    public string? AuditEntityId => StorageNo;
}
public sealed class AdmitBodyHandler : MediatR.IRequestHandler<AdmitBodyCommand, long>
{
    private readonly IStatutoryRepository _r; private readonly IPatientRepository _patients; private readonly IBranchContext _ctx;
    public AdmitBodyHandler(IStatutoryRepository r, IPatientRepository patients, IBranchContext ctx) { _r = r; _patients = patients; _ctx = ctx; }
    public async Task<long> Handle(AdmitBodyCommand c, CancellationToken ct)
    {
        long? patientId = string.IsNullOrWhiteSpace(c.PatientUhid) ? null
            : (await _patients.GetByUhidAsync(LookupCode.Parse(c.PatientUhid!), ct))?.PatientId;
        return await _r.InsertMortuaryAsync(new MortuaryRecord
        {
            BranchId = _ctx.BranchId ?? 0, PatientId = patientId, StorageNo = c.StorageNo,
            AdmittedUtc = DateTime.UtcNow, MlcLinked = c.MlcLinked, PoliceIntimated = c.PoliceIntimated
        }, ct);
    }
}
public sealed record ReleaseBodyCommand(long RecordId) : ICommand<bool>, IAuditable
{
    public string AuditEntity => "MortuaryRecord";
    public string? AuditEntityId => RecordId.ToString();
}
public sealed class ReleaseBodyHandler : MediatR.IRequestHandler<ReleaseBodyCommand, bool>
{
    private readonly IStatutoryRepository _r;
    public ReleaseBodyHandler(IStatutoryRepository r) { _r = r; }
    public async Task<bool> Handle(ReleaseBodyCommand c, CancellationToken ct) { await _r.ReleaseMortuaryAsync(c.RecordId, ct); return true; }
}
public sealed record MortuaryRowDto(long RecordId, string? Patient, string? StorageNo, string Admitted, string? Released, bool Mlc);
public sealed record GetMortuaryQuery : IQuery<IReadOnlyList<MortuaryRowDto>>;
public sealed class GetMortuaryHandler : MediatR.IRequestHandler<GetMortuaryQuery, IReadOnlyList<MortuaryRowDto>>
{
    private readonly IStatutoryRepository _r; private readonly IBranchContext _ctx;
    public GetMortuaryHandler(IStatutoryRepository r, IBranchContext ctx) { _r = r; _ctx = ctx; }
    public async Task<IReadOnlyList<MortuaryRowDto>> Handle(GetMortuaryQuery q, CancellationToken ct)
        => (await _r.GetMortuaryAsync(_ctx.BranchId ?? 0, ct)).Select(m => new MortuaryRowDto(m.RecordId, m.Patient, m.StorageNo, m.Admitted, m.Released, m.Mlc)).ToList();
}

// ============================ MLC (SRS §3.28) ============================
public sealed record CreateMlcCommand(string? PatientUhid, string? PoliceStation, string? InjuryDetails) : ICommand<CreateMlcResult>, IAuditable
{
    public string AuditEntity => "MlcCase";
    public string? AuditEntityId => PatientUhid;
}
public sealed record CreateMlcResult(long MlcId, string MlcNo);
public sealed class CreateMlcHandler : MediatR.IRequestHandler<CreateMlcCommand, CreateMlcResult>
{
    private readonly IStatutoryRepository _r; private readonly IPatientRepository _patients; private readonly IBranchContext _ctx;
    public CreateMlcHandler(IStatutoryRepository r, IPatientRepository patients, IBranchContext ctx) { _r = r; _patients = patients; _ctx = ctx; }
    public async Task<CreateMlcResult> Handle(CreateMlcCommand c, CancellationToken ct)
    {
        var branchId = _ctx.BranchId ?? throw new InvalidOperationException("Branch context required.");
        long? patientId = string.IsNullOrWhiteSpace(c.PatientUhid) ? null
            : (await _patients.GetByUhidAsync(LookupCode.Parse(c.PatientUhid!), ct))?.PatientId;
        var mlcNo = await _r.NextMlcNoAsync(branchId, ct);
        var id = await _r.InsertMlcAsync(new MlcCase
        {
            MlcNo = mlcNo, BranchId = branchId, PatientId = patientId, PoliceStation = c.PoliceStation,
            InjuryDetails = c.InjuryDetails, CreatedUtc = DateTime.UtcNow
        }, ct);
        return new CreateMlcResult(id, mlcNo);
    }
}
public sealed record IntimatePoliceCommand(long MlcId, string AckRef) : ICommand<bool>, IAuditable
{
    public string AuditEntity => "MlcCase";
    public string? AuditEntityId => MlcId.ToString();
}
public sealed class IntimatePoliceValidator : AbstractValidator<IntimatePoliceCommand>
{
    public IntimatePoliceValidator() { RuleFor(x => x.MlcId).GreaterThan(0); RuleFor(x => x.AckRef).NotEmpty(); }
}
public sealed class IntimatePoliceHandler : MediatR.IRequestHandler<IntimatePoliceCommand, bool>
{
    private readonly IStatutoryRepository _r;
    public IntimatePoliceHandler(IStatutoryRepository r) { _r = r; }
    public async Task<bool> Handle(IntimatePoliceCommand c, CancellationToken ct) { await _r.IntimatePoliceAsync(c.MlcId, c.AckRef, ct); return true; }
}
public sealed record MlcRowDto(long MlcId, string MlcNo, string? Patient, string? PoliceStation, string? PoliceAck, string Created);
public sealed record GetMlcQuery : IQuery<IReadOnlyList<MlcRowDto>>;
public sealed class GetMlcHandler : MediatR.IRequestHandler<GetMlcQuery, IReadOnlyList<MlcRowDto>>
{
    private readonly IStatutoryRepository _r; private readonly IBranchContext _ctx;
    public GetMlcHandler(IStatutoryRepository r, IBranchContext ctx) { _r = r; _ctx = ctx; }
    public async Task<IReadOnlyList<MlcRowDto>> Handle(GetMlcQuery q, CancellationToken ct)
        => (await _r.GetMlcAsync(_ctx.BranchId ?? 0, ct)).Select(m => new MlcRowDto(m.MlcId, m.MlcNo, m.Patient, m.PoliceStation, m.PoliceAck, m.Created)).ToList();
}
