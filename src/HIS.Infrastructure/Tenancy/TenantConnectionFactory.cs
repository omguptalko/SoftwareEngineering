using System.Data;
using Dapper;
using HIS.Application.Abstractions;
using HIS.Shared.Context;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace HIS.Infrastructure.Tenancy;

/// <summary>
/// Opens connections to the resolved tenant's master / current-FY data DB (L1.6).
/// DB names come from the per-request <see cref="ITenantContext"/>; the connection
/// string is built from "Provisioning:BaseConnection" with the catalog swapped.
/// </summary>
public sealed class TenantConnectionFactory : ITenantConnectionFactory
{
    private readonly ITenantContext _tenant;
    private readonly string _baseConnection;

    public TenantConnectionFactory(ITenantContext tenant, IConfiguration config)
    {
        _tenant = tenant;
        _baseConnection = config["Provisioning:BaseConnection"]
            ?? config.GetConnectionString("Platform")
            ?? throw new InvalidOperationException("Missing 'Provisioning:BaseConnection' / 'ConnectionStrings:Platform'.");
    }

    public Task<IDbConnection> OpenMasterAsync(CancellationToken ct = default)
    {
        if (!_tenant.IsResolved || string.IsNullOrWhiteSpace(_tenant.MasterDb))
            throw new InvalidOperationException("No tenant resolved for this request (master DB unavailable).");
        return OpenAsync(_tenant.MasterDb!, ct);
    }

    public Task<IDbConnection> OpenDataAsync(CancellationToken ct = default)
    {
        if (!_tenant.IsResolved)
            throw new InvalidOperationException("No tenant resolved for this request.");
        if (string.IsNullOrWhiteSpace(_tenant.DataDb))
            throw new InvalidOperationException($"Tenant '{_tenant.TenantCode}' has no current fiscal-year database.");
        return OpenAsync(_tenant.DataDb!, ct);
    }

    private async Task<IDbConnection> OpenAsync(string database, CancellationToken ct)
    {
        var cs = new SqlConnectionStringBuilder(_baseConnection) { InitialCatalog = database }.ConnectionString;
        var conn = new SqlConnection(cs);
        await conn.OpenAsync(ct);
        return conn;
    }
}

/// <summary>Tenant-routed demo data access (patients → master DB, bills → current-FY DB).</summary>
public sealed class TenantScopedRepository : ITenantScopedRepository
{
    private readonly ITenantConnectionFactory _f;
    private readonly ITenantContext _tenant;
    public TenantScopedRepository(ITenantConnectionFactory f, ITenantContext tenant) { _f = f; _tenant = tenant; }

    public async Task<(long, string)> AddPatientAsync(string fullName, string? mobile, CancellationToken ct = default)
    {
        using var c = await _f.OpenMasterAsync(ct);
        var uhid = await c.QuerySingleAsync<string>(new CommandDefinition(
            "EXEC [proc].usp_NextUhid @BranchId=1", cancellationToken: ct));
        var id = await c.QuerySingleAsync<long>(new CommandDefinition(
            @"INSERT INTO patient.Patient (Uhid, FullName, Mobile) VALUES (@uhid, @fullName, @mobile);
              SELECT CAST(SCOPE_IDENTITY() AS BIGINT);",
            new { uhid, fullName, mobile }, cancellationToken: ct));
        return (id, uhid);
    }

    public async Task<IReadOnlyList<(long, string, string)>> GetPatientsAsync(CancellationToken ct = default)
    {
        using var c = await _f.OpenMasterAsync(ct);
        var rows = await c.QueryAsync<(long, string, string)>(new CommandDefinition(
            "SELECT PatientId, Uhid, FullName FROM patient.Patient ORDER BY PatientId", cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<(long, string)> AddBillAsync(decimal gross, CancellationToken ct = default)
    {
        using var c = await _f.OpenDataAsync(ct);
        var billNo = await c.QuerySingleAsync<string>(new CommandDefinition(
            "EXEC [proc].usp_NextDocNo @BranchId=1, @DocType='BILL', @Prefix='BILL', @FyCode=@fy",
            new { fy = _tenant.FiscalYearCode }, cancellationToken: ct));
        var id = await c.QuerySingleAsync<long>(new CommandDefinition(
            @"INSERT INTO billing.Bill (BillNo, Gross, PatientPays, Status)
              VALUES (@billNo, @gross, @gross, 'Open');
              SELECT CAST(SCOPE_IDENTITY() AS BIGINT);",
            new { billNo, gross }, cancellationToken: ct));
        return (id, billNo);
    }

    public async Task<IReadOnlyList<(long, string, decimal, string)>> GetBillsAsync(CancellationToken ct = default)
    {
        using var c = await _f.OpenDataAsync(ct);
        var rows = await c.QueryAsync<(long, string, decimal, string)>(new CommandDefinition(
            "SELECT BillId, BillNo, Gross, Status FROM billing.Bill ORDER BY BillId", cancellationToken: ct));
        return rows.ToList();
    }
}
