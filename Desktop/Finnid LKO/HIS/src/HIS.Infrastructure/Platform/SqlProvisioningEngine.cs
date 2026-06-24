using System.Text;
using System.Text.RegularExpressions;
using Dapper;
using HIS.Application.Abstractions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace HIS.Infrastructure.Platform;

/// <summary>
/// Creates per-tenant master + per-fiscal-year data databases and applies their
/// schema templates (L1.5, R4) — no human intervention. Settings from "Provisioning:*":
///   BaseConnection  : connection whose InitialCatalog is swapped per target DB
///                     (falls back to ConnectionStrings:Platform). Decision D5.
///   TemplateRoot    : folder holding master/ and fy/ *.sql templates
///                     (resolved against the current directory if relative).
/// DB names are validated to ^[A-Za-z][A-Za-z0-9_]*$ before any CREATE DATABASE.
/// </summary>
public sealed class SqlProvisioningEngine : IProvisioningEngine
{
    private static readonly Regex SafeName = new(@"^[A-Za-z][A-Za-z0-9_]*$", RegexOptions.Compiled);

    private readonly string _baseConnection;
    private readonly string _templateRoot;

    public SqlProvisioningEngine(IConfiguration config)
    {
        _baseConnection = config["Provisioning:BaseConnection"]
            ?? config.GetConnectionString("Platform")
            ?? throw new InvalidOperationException("Missing 'Provisioning:BaseConnection' / 'ConnectionStrings:Platform'.");

        var root = config["Provisioning:TemplateRoot"] ?? "db/tenant-template";
        _templateRoot = Path.IsPathRooted(root) ? root : Path.Combine(Directory.GetCurrentDirectory(), root);
    }

    public Task<ProvisionedDb> ProvisionMasterAsync(string tenantCode, CancellationToken ct = default)
        => ProvisionAsync("master", DbName(tenantCode, "Master"), Path.Combine(_templateRoot, "master"), null, ct);

    public Task<ProvisionedDb> ProvisionFiscalYearAsync(string tenantCode, string fyCode, CancellationToken ct = default)
        => ProvisionAsync("data", DbName(tenantCode, fyCode), Path.Combine(_templateRoot, "fy"), null, ct);

    private async Task<ProvisionedDb> ProvisionAsync(string dbKind, string dbName, string scriptFolder, string? _, CancellationToken ct)
    {
        if (!SafeName.IsMatch(dbName))
            throw new InvalidOperationException($"Refusing to provision unsafe database name '{dbName}'.");
        if (!Directory.Exists(scriptFolder))
            throw new InvalidOperationException($"Provisioning template folder not found: '{scriptFolder}'.");

        await CreateDatabaseIfNotExistsAsync(dbName, ct);

        var targetConn = WithCatalog(dbName);
        foreach (var file in Directory.GetFiles(scriptFolder, "*.sql").OrderBy(f => f, StringComparer.Ordinal))
            await RunScriptAsync(targetConn, await File.ReadAllTextAsync(file, ct), ct);

        return new ProvisionedDb(dbKind, dbName);
    }

    private async Task CreateDatabaseIfNotExistsAsync(string dbName, CancellationToken ct)
    {
        await using var c = new SqlConnection(WithCatalog("master"));
        await c.OpenAsync(ct);
        // dbName already validated against SafeName; QUOTENAME guards it. EXEC needs a
        // variable (it won't accept a function call inline).
        await c.ExecuteAsync(new CommandDefinition(
            @"IF DB_ID(@n) IS NULL
              BEGIN
                  DECLARE @sql NVARCHAR(300) = N'CREATE DATABASE ' + QUOTENAME(@n);
                  EXEC(@sql);
              END",
            new { n = dbName }, cancellationToken: ct));
    }

    private static async Task RunScriptAsync(string connectionString, string script, CancellationToken ct)
    {
        await using var c = new SqlConnection(connectionString);
        await c.OpenAsync(ct);
        foreach (var batch in SplitOnGo(script))
            await c.ExecuteAsync(new CommandDefinition(batch, cancellationToken: ct));
    }

    /// <summary>Splits a T-SQL script on lines that are just "GO" (sqlcmd batch separator).</summary>
    private static IEnumerable<string> SplitOnGo(string script)
    {
        var sb = new StringBuilder();
        foreach (var line in script.Split('\n'))
        {
            if (Regex.IsMatch(line.Trim(), @"^GO\s*;?$", RegexOptions.IgnoreCase))
            {
                var batch = sb.ToString().Trim();
                if (batch.Length > 0) yield return batch;
                sb.Clear();
            }
            else sb.Append(line).Append('\n');
        }
        var last = sb.ToString().Trim();
        if (last.Length > 0) yield return last;
    }

    private string WithCatalog(string database) =>
        new SqlConnectionStringBuilder(_baseConnection) { InitialCatalog = database }.ConnectionString;

    private static string DbName(string tenantCode, string suffix)
    {
        var safeTenant = Regex.Replace(tenantCode ?? "", @"[^A-Za-z0-9]", "_");
        var safeSuffix = Regex.Replace(suffix ?? "", @"[^A-Za-z0-9]", "_");
        var name = $"{safeTenant}_{safeSuffix}";
        if (name.Length > 0 && !char.IsLetter(name[0])) name = "T" + name;   // ensure leading letter
        return name;
    }
}
