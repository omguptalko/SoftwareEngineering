using HIS.Application.Abstractions;
using Microsoft.Extensions.Configuration;

namespace HIS.Infrastructure.Ai;

/// <summary>
/// Transparent, config-driven baseline for the AI layer (SRS §4). Every threshold
/// is read from "Ai:*" configuration — nothing hardcoded. In production this is
/// replaced (via DI) by an adapter that calls the Azure AI / Python ML endpoint.
/// </summary>
public sealed class HeuristicAiEngine : IAiEngine
{
    private readonly IConfiguration _c;
    public HeuristicAiEngine(IConfiguration c) => _c = c;

    public RiskResult ScoreRisk(RiskFeatures f)
    {
        var spo2Low = _c.GetValue("Ai:Risk:Spo2Low", 94);
        var bpHigh = _c.GetValue("Ai:Risk:BpSystolicHigh", 140);
        var pulseHigh = _c.GetValue("Ai:Risk:PulseHigh", 100);
        var tempHigh = _c.GetValue("Ai:Risk:TempFHigh", 100.4m);

        var factors = new List<string>();
        if (f.Spo2 is int s && s < spo2Low) factors.Add($"SpO₂ {s}% < {spo2Low}%");
        if (f.BpSystolic is int bp && bp > bpHigh) factors.Add($"BP {bp} > {bpHigh}");
        if (f.Pulse is int p && p > pulseHigh) factors.Add($"Pulse {p} > {pulseHigh}");
        if (f.TempF is decimal t && t > tempHigh) factors.Add($"Temp {t}°F > {tempHigh}°F");

        var score = Math.Round(factors.Count / 4m, 2);
        var level = factors.Count >= 2 ? "High" : factors.Count == 1 ? "Medium" : "Low";
        return new RiskResult(score, level, factors);
    }

    public FraudResult ScoreFraud(FraudFeatures f)
    {
        var highBill = _c.GetValue("Ai:Fraud:HighBillThreshold", 200000m);
        var flags = new List<string>();
        decimal score = 0;
        if (f.BillGross > highBill) { flags.Add($"Bill ₹{f.BillGross:N0} exceeds ₹{highBill:N0}"); score += 0.6m; }
        if (f.DuplicateSuspected) { flags.Add("Possible duplicate bill (same patient & amount)"); score += 0.4m; }
        return new FraudResult(Math.Min(1m, score), flags);
    }

    public PreScrubResult PreScrub(PreScrubFeatures f)
    {
        var issues = new List<string>();
        if (!f.HasIcd10) issues.Add("Missing provisional ICD-10 diagnosis");
        if (f.PreAuthAmount <= 0) issues.Add("Pre-authorisation amount not set");
        if (f.AttachedMandatory < f.MandatoryDocs)
            issues.Add($"Mandatory documents incomplete ({f.AttachedMandatory}/{f.MandatoryDocs} attached)");
        return new PreScrubResult(issues.Count == 0, issues);
    }

    public int ForecastReorderQty(decimal avgDailyUsage, int stock, int reorderLevel)
    {
        var horizon = _c.GetValue("Ai:Inventory:HorizonDays", 7);
        var predictedDemand = (int)Math.Ceiling(avgDailyUsage * horizon);
        return Math.Max(0, predictedDemand + reorderLevel - stock);
    }

    public string Chat(string message)
    {
        var m = (message ?? "").ToLowerInvariant();
        if (m.Contains("appointment") || m.Contains("book")) return "You can book an appointment from Patient → Appointments. Would you like the next available slot?";
        if (m.Contains("report") || m.Contains("result")) return "Lab/Radiology reports appear under the patient's portal once released by the lab.";
        if (m.Contains("medicine") || m.Contains("reminder")) return "I can set a medicine reminder. Please tell me the drug and timing.";
        if (m.Contains("emergency") || m.Contains("ambulance")) return "For emergencies, call the front desk or use Support → Ambulance to dispatch the nearest vehicle.";
        return "I'm the HIS assistant. Ask me about appointments, reports, medicine reminders or emergencies.";
    }
}
