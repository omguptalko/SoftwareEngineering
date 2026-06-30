using HIS.Application.Abstractions;
using Microsoft.Extensions.Configuration;

namespace HIS.Application.Features.Ai;

/// <summary>Vitals input for the early-warning risk score (any field may be null = not measured).</summary>
public sealed record RiskVitalsInput(
    int? RespiratoryRate, int? SpO2, decimal? TemperatureC, int? SystolicBp, int? HeartRate, string? Consciousness);

public sealed record RiskFlag(string Parameter, int Points, string Note);
public sealed record RiskResult(int Score, string Band, string Recommendation, IReadOnlyList<RiskFlag> Flags, string Model);

/// <summary>
/// AI Patient Risk Prediction (SRS 4.1, Phase 11.1). A transparent, deterministic
/// NEWS2-style aggregate early-warning score computed from vitals - an explainable
/// baseline behind the same request/response seam an Azure ML model would use
/// (swap by config later). Band cut-offs are config-driven (Ai:Risk:*), not hardcoded.
/// </summary>
public sealed record AssessPatientRiskQuery(RiskVitalsInput Vitals) : IQuery<RiskResult>, IRequireAuthentication;

public sealed class AssessPatientRiskHandler : MediatR.IRequestHandler<AssessPatientRiskQuery, RiskResult>
{
    private readonly IConfiguration _config;
    public AssessPatientRiskHandler(IConfiguration config) => _config = config;

    public Task<RiskResult> Handle(AssessPatientRiskQuery request, CancellationToken ct)
    {
        var v = request.Vitals;
        var flags = new List<RiskFlag>();
        int score = 0;
        void add(string param, int pts, string note) { if (pts > 0) { score += pts; flags.Add(new RiskFlag(param, pts, note)); } }

        // NEWS2 aggregate sub-scores (RCP national standard; encoded like a reference table).
        if (v.RespiratoryRate is { } rr)
            add("Respiratory rate", rr <= 8 ? 3 : rr <= 11 ? 1 : rr <= 20 ? 0 : rr <= 24 ? 2 : 3, $"{rr}/min");
        if (v.SpO2 is { } sp)
            add("SpO2", sp >= 96 ? 0 : sp >= 94 ? 1 : sp >= 92 ? 2 : 3, $"{sp}%");
        if (v.TemperatureC is { } t)
            add("Temperature", t <= 35m ? 3 : t <= 36m ? 1 : t <= 38m ? 0 : t <= 39m ? 1 : 2, $"{t}C");
        if (v.SystolicBp is { } sbp)
            add("Systolic BP", sbp <= 90 ? 3 : sbp <= 100 ? 2 : sbp <= 110 ? 1 : sbp <= 219 ? 0 : 3, $"{sbp} mmHg");
        if (v.HeartRate is { } hr)
            add("Heart rate", hr <= 40 ? 3 : hr <= 50 ? 1 : hr <= 90 ? 0 : hr <= 110 ? 1 : hr <= 130 ? 2 : 3, $"{hr} bpm");
        var conscious = (v.Consciousness ?? "Alert").Trim();
        if (!conscious.Equals("Alert", StringComparison.OrdinalIgnoreCase) && conscious.Length > 0)
            add("Consciousness", 3, conscious);

        // Config-driven band cut-offs (standard NEWS2 trigger points as defaults).
        var medium = _config.GetValue("Ai:Risk:MediumScore", 5);
        var high = _config.GetValue("Ai:Risk:HighScore", 7);
        var anyRedZone = flags.Any(f => f.Points >= 3);

        string band, rec;
        if (score >= high) { band = "High"; rec = "Urgent clinical review - consider critical-care escalation."; }
        else if (score >= medium || anyRedZone) { band = "Medium"; rec = "Prompt review by a clinician; increase monitoring frequency."; }
        else if (score >= 1) { band = "Low-Medium"; rec = "Routine monitoring; reassess per ward protocol."; }
        else { band = "Low"; rec = "Continue routine observations."; }

        return Task.FromResult(new RiskResult(score, band, rec, flags, "NEWS2-aggregate-v1 (deterministic)"));
    }
}
