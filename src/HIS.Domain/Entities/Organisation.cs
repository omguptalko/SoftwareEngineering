namespace HIS.Domain.Entities;

/// <summary>Hospital branch — SRS §3.21 multi-branch. Source of all branch context.</summary>
public sealed class Branch
{
    public int BranchId { get; set; }
    public string Code { get; set; } = "";      // e.g. BR1
    public string Name { get; set; } = "";       // e.g. Indl-North
    public string? City { get; set; }
    public string? State { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>Sidebar module group — drives the wireframe explorer (was static in data.js).</summary>
public sealed class ModuleGroup
{
    public string GroupId { get; set; } = "";
    public string Label { get; set; } = "";
    public string Icon { get; set; } = "";
    public int SortOrder { get; set; }
}

/// <summary>A functional module from SRS §3/§4/§5/§6/§7. Registry was static in data.js.</summary>
public sealed class Module
{
    public string ModuleId { get; set; } = "";
    public string GroupId { get; set; } = "";
    public string Icon { get; set; } = "";
    public string Label { get; set; } = "";
    public bool Built { get; set; }
    public string? Badge { get; set; }     // e.g. NEW
    public int SortOrder { get; set; }
    public string? SrsRef { get; set; }    // e.g. §3.1
}
