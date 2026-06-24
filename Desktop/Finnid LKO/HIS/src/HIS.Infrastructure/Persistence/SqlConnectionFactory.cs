using System.Data;
using HIS.Application.Abstractions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace HIS.Infrastructure.Persistence;

/// <summary>
/// Opens MS SQL connections for Dapper. Connection string is read from config
/// (appsettings / environment / Key Vault) under "ConnectionStrings:His" —
/// never hardcoded (SRS §8.1/§8.2 "nothing hardcoded").
/// </summary>
public sealed class SqlConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public SqlConnectionFactory(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("His")
            ?? throw new InvalidOperationException("Missing connection string 'ConnectionStrings:His'.");
    }

    public async Task<IDbConnection> CreateOpenConnectionAsync(CancellationToken ct = default)
    {
        var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        return conn;
    }
}
