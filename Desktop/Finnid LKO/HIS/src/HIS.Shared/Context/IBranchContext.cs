namespace HIS.Shared.Context;

/// <summary>
/// Per-request branch + user context. Resolved from the authenticated principal
/// (claims), never assumed or hardcoded — supports SRS §3.21 multi-branch.
/// </summary>
public interface IBranchContext
{
    int? BranchId { get; }
    string? BranchCode { get; }
    long? UserId { get; }
    string? UserName { get; }
    IReadOnlyCollection<string> Roles { get; }
    bool IsAuthenticated { get; }
}

/// <summary>Mutable default implementation populated by the API/Web pipeline.</summary>
public sealed class BranchContext : IBranchContext
{
    public int? BranchId { get; set; }
    public string? BranchCode { get; set; }
    public long? UserId { get; set; }
    public string? UserName { get; set; }
    public IReadOnlyCollection<string> Roles { get; set; } = Array.Empty<string>();
    public bool IsAuthenticated => UserId.HasValue;
}
