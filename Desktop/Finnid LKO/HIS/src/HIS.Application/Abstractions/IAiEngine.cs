namespace HIS.Application.Abstractions;

// Feature/result contracts for the AI layer (SRS §4). Inputs are extracted from
// the DB by handlers; the engine turns them into scores/decisions.
public sealed record RiskFeatures(int? Spo2, int? BpSystolic, int? Pulse, decimal? TempF);
public sealed record RiskResult(decimal Score, string Level, IReadOnlyList<string> Factors);

public sealed record FraudFeatures(decimal BillGross, bool DuplicateSuspected);
public sealed record FraudResult(decimal Score, IReadOnlyList<string> Flags);

public sealed record PreScrubFeatures(bool HasIcd10, decimal PreAuthAmount, int MandatoryDocs, int AttachedMandatory);
public sealed record PreScrubResult(bool Pass, IReadOnlyList<string> Issues);

/// <summary>
/// The AI model adapter (SRS §4 / tech stack: Azure AI Services + Python ML).
/// The default implementation is a transparent, config-driven heuristic baseline;
/// it is swapped for the real Azure AI / Python ML endpoint via DI — no thresholds
/// are hardcoded (they come from "Ai:*" configuration).
/// </summary>
public interface IAiEngine
{
    RiskResult ScoreRisk(RiskFeatures f);
    FraudResult ScoreFraud(FraudFeatures f);
    PreScrubResult PreScrub(PreScrubFeatures f);
    int ForecastReorderQty(decimal avgDailyUsage, int stock, int reorderLevel);
    string Chat(string message);
}

public interface IAiRepository
{
    Task<long> SaveInsightAsync(Domain.Entities.AiInsight insight, CancellationToken ct = default);
    Task<IReadOnlyList<(string Type, string? Subject, decimal? Score, string? Detail, string Generated)>> GetInsightsAsync(int branchId, CancellationToken ct = default);
    Task<RiskFeatures?> GetLatestVitalsAsync(long patientId, CancellationToken ct = default);
    Task<IReadOnlyList<(int DrugId, string Code, string Name, int Stock, int ReorderLevel, decimal AvgDailyUsage)>> GetDrugUsageAsync(int branchId, int windowDays, CancellationToken ct = default);
    Task<(bool HasIcd10, decimal PreAuth, int MandatoryDocs, int AttachedMandatory)?> GetClaimScrubDataAsync(long claimId, CancellationToken ct = default);
    Task<IReadOnlyList<(string BillNo, decimal Gross, long PatientId)>> GetRecentBillsAsync(int branchId, CancellationToken ct = default);
}
