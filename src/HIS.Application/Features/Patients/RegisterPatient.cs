using FluentValidation;
using HIS.Application.Abstractions;
using HIS.Domain.Entities;
using HIS.Shared.Context;

namespace HIS.Application.Features.Patients;

/// <summary>Registers a new patient and generates a branch-scoped UHID (SRS §3.1).</summary>
public sealed record RegisterPatientCommand(
    string FullName, string? GuardianName, int? AgeYears, DateTime? DateOfBirth, string Sex,
    string? BloodGroup, string Mobile, string? Email, string? MaritalStatus, string? Category,
    string? Address, string? City, string? State, string? Pincode, string? Occupation,
    string? EmployerPayerCode, string? AadhaarMasked, string? AbhaNumber, string? AbhaAddress)
    : ICommand<RegisterPatientResult>, IAuditable
{
    public string AuditEntity => "Patient";
    public string? AuditEntityId => Mobile;   // pre-insert identifier
}

public sealed record RegisterPatientResult(long PatientId, string Uhid);

public sealed class RegisterPatientValidator : AbstractValidator<RegisterPatientCommand>
{
    public RegisterPatientValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Sex).NotEmpty();
        RuleFor(x => x.Mobile).NotEmpty().Matches(@"^\d{10}$").WithMessage("Mobile must be 10 digits.");
        RuleFor(x => x.AgeYears).GreaterThanOrEqualTo(0).When(x => x.AgeYears.HasValue);
    }
}

public sealed class RegisterPatientHandler : MediatR.IRequestHandler<RegisterPatientCommand, RegisterPatientResult>
{
    private readonly IPatientRepository _repo;
    private readonly IBranchContext _ctx;

    public RegisterPatientHandler(IPatientRepository repo, IBranchContext ctx)
    {
        _repo = repo; _ctx = ctx;
    }

    public async Task<RegisterPatientResult> Handle(RegisterPatientCommand c, CancellationToken ct)
    {
        var branchId = _ctx.BranchId ?? throw new InvalidOperationException("Branch context required.");

        // De-duplication on Aadhaar (SRS §6.1) — never register the same Aadhaar twice.
        if (!string.IsNullOrWhiteSpace(c.AadhaarMasked) && await _repo.AadhaarExistsAsync(c.AadhaarMasked!, ct))
            throw new InvalidOperationException("Duplicate Aadhaar — patient already registered.");

        var uhid = await _repo.GetNextUhidAsync(branchId, ct);

        var patient = new Patient
        {
            Uhid = uhid,
            RegBranchId = branchId,
            RegisteredAtUtc = DateTime.UtcNow,
            FullName = c.FullName,
            GuardianName = c.GuardianName,
            AgeYears = c.AgeYears,
            DateOfBirth = c.DateOfBirth,
            Sex = c.Sex,
            BloodGroup = c.BloodGroup,
            Mobile = c.Mobile,
            Email = c.Email,
            MaritalStatus = c.MaritalStatus,
            Category = c.Category,
            Address = c.Address,
            City = c.City,
            State = c.State,
            Pincode = c.Pincode,
            Occupation = c.Occupation,
            EmployerPayerCode = c.EmployerPayerCode,
            AadhaarMasked = c.AadhaarMasked,
            AbhaNumber = c.AbhaNumber,
            AbhaAddress = c.AbhaAddress,
            IsActive = true
        };

        var id = await _repo.InsertAsync(patient, ct);
        return new RegisterPatientResult(id, uhid);
    }
}
