namespace HIS.Domain.Entities;

/// <summary>Stored AI output — SRS §4. The model itself (Azure AI / Python ML) is
/// consumed via the IAiEngine adapter; this table holds the produced insights.</summary>
public sealed class AiInsight
{
    public long InsightId { get; set; }
    public int BranchId { get; set; }
    public string InsightType { get; set; } = "";    // RiskPrediction/InventoryForecast/FraudDetection/ClaimPreScrub/Chatbot/SmartScheduling
    public string? SubjectType { get; set; }
    public string? SubjectId { get; set; }
    public decimal? Score { get; set; }
    public string? DetailJson { get; set; }
    public DateTime GeneratedUtc { get; set; }
}
