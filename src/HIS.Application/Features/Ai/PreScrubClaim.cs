using HIS.Application.Abstractions;
using Microsoft.Extensions.Configuration;

namespace HIS.Application.Features.Ai;

public sealed record PreScrubInput(string? PatientUhid, string? PackageCode, decimal ClaimedAmount, IReadOnlyList<string>? Documents);

/// <param name="Severity">pass | warn | fail</param>
public sealed record PreScrubCheck(string Severity, string Rule, string Detail);

public sealed record PreScrubResult(
    string Verdict, int Passed, int Warnings, int Failures,
    decimal? PackageRate, decimal? AvailableBalance, decimal? EstimatedCoPay,
    IReadOnlyList<PreScrubCheck> Checks);

/// <summary>
/// AI Claim Pre-Scrubbing (SRS 4.6, Phase 11.6). Validates a pre-auth/claim against
/// payer/package rules BEFORE submission to cut denials: package rate ceiling, policy
/// balance and sum-insured, co-pay estimate, and required-document completeness. A
/// transparent rule engine behind the AI seam; required docs are config-driven
/// (Ai:PreScrub:RequiredDocs). Reuses the existing claims/package/patient repositories.
/// </summary>
public sealed record PreScrubClaimQuery(PreScrubInput Input) : IQuery<PreScrubResult>, IRequireAuthentication;

public sealed class PreScrubClaimHandler : MediatR.IRequestHandler<PreScrubClaimQuery, PreScrubResult>
{
    private readonly IPatientRepository _patients;
    private readonly IClaimsRepository _claims;
    private readonly IPmjayRepository _pmjay;
    private readonly IConfiguration _config;

    public PreScrubClaimHandler(IPatientRepository patients, IClaimsRepository claims, IPmjayRepository pmjay, IConfiguration config)
    {
        _patients = patients; _claims = claims; _pmjay = pmjay; _config = config;
    }

    public async Task<PreScrubResult> Handle(PreScrubClaimQuery request, CancellationToken ct)
    {
        var i = request.Input;
        var checks = new List<PreScrubCheck>();
        void pass(string r, string d) => checks.Add(new PreScrubCheck("pass", r, d));
        void warn(string r, string d) => checks.Add(new PreScrubCheck("warn", r, d));
        void fail(string r, string d) => checks.Add(new PreScrubCheck("fail", r, d));

        // Amount sanity.
        if (i.ClaimedAmount <= 0) fail("Amount", "Claimed amount must be greater than zero.");
        else pass("Amount", $"Claimed amount {i.ClaimedAmount:N0}.");

        // Package-rate ceiling (PM-JAY HBP master).
        decimal? packageRate = null;
        if (!string.IsNullOrWhiteSpace(i.PackageCode))
        {
            var pkg = await _pmjay.GetPackageByCodeAsync(i.PackageCode!, ct);
            if (pkg is null) fail("Package", $"Unknown package code '{i.PackageCode}'.");
            else
            {
                packageRate = pkg.Value.Rate;
                if (i.ClaimedAmount > packageRate)
                    fail("Package rate", $"Claimed {i.ClaimedAmount:N0} exceeds package {i.PackageCode} rate {packageRate:N0}.");
                else
                    pass("Package rate", $"Within package {i.PackageCode} rate {packageRate:N0}.");
            }
        }
        else warn("Package", "No package code supplied - package-rate check skipped.");

        // Policy balance / sum-insured / co-pay.
        decimal? availableBalance = null, coPay = null;
        if (!string.IsNullOrWhiteSpace(i.PatientUhid))
        {
            var patient = await _patients.GetByUhidAsync(i.PatientUhid!, ct);
            if (patient is null) warn("Policy", $"Patient '{i.PatientUhid}' not found - policy checks skipped.");
            else
            {
                var policies = await _claims.GetPoliciesAsync(patient.PatientId, ct);
                var policy = policies.FirstOrDefault();
                if (policy.PolicyId == 0) warn("Policy", "No active insurance policy on file - treat as cash/scheme.");
                else
                {
                    availableBalance = policy.AvailableBalance;
                    if (policy.SumInsured is { } si && i.ClaimedAmount > si)
                        fail("Sum insured", $"Claimed {i.ClaimedAmount:N0} exceeds sum insured {si:N0}.");
                    if (availableBalance is { } bal)
                    {
                        if (i.ClaimedAmount > bal) fail("Balance", $"Claimed {i.ClaimedAmount:N0} exceeds available balance {bal:N0}.");
                        else pass("Balance", $"Within available balance {bal:N0}.");
                    }
                    if (policy.CoPayPct is { } cp && cp > 0)
                    {
                        coPay = Math.Round(i.ClaimedAmount * cp / 100m, 2);
                        warn("Co-pay", $"Patient co-pay {cp:N0}% = {coPay:N0} payable by patient.");
                    }
                }
            }
        }
        else warn("Policy", "No patient supplied - policy checks skipped.");

        // Required-document completeness (config-driven).
        var required = (_config["Ai:PreScrub:RequiredDocs"] ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (required.Length > 0)
        {
            var provided = new HashSet<string>(i.Documents ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            var missing = required.Where(r => !provided.Contains(r)).ToList();
            if (missing.Count == 0) pass("Documents", "All required documents attached.");
            else missing.ForEach(m => warn("Documents", $"Missing required document: {m}."));
        }

        var failures = checks.Count(c => c.Severity == "fail");
        var warnings = checks.Count(c => c.Severity == "warn");
        var passed = checks.Count(c => c.Severity == "pass");
        var verdict = failures > 0 ? "Reject" : warnings > 0 ? "Review" : "Clean";

        return new PreScrubResult(verdict, passed, warnings, failures, packageRate, availableBalance, coPay, checks);
    }
}
