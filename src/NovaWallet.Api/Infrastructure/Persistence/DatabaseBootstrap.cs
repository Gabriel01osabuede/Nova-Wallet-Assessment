using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace NovaWallet.Api.Infrastructure.Persistence;

public static partial class DatabaseBootstrap
{
    [GeneratedRegex("^[A-Za-z][A-Za-z0-9_]{0,63}$")]
    private static partial Regex DatabaseNamePattern();

    public static async Task InitializeAsync(string privilegedConnection, string runtimePassword, CancellationToken ct = default)
    {
        var target = new SqlConnectionStringBuilder(privilegedConnection);
        var database = target.InitialCatalog;
        if (!DatabaseNamePattern().IsMatch(database))
            throw new ArgumentException("Bootstrap database name must be a simple SQL identifier.");
        if (runtimePassword.Length < 16) throw new ArgumentException("Runtime password must contain at least 16 characters.");
        var master = new SqlConnectionStringBuilder(privilegedConnection) { InitialCatalog = "master" };
        await using (var connection = new SqlConnection(master.ConnectionString))
        {
            await connection.OpenAsync(ct);
            await using var create = connection.CreateCommand();
            create.CommandText = $"IF DB_ID(@Name) IS NULL CREATE DATABASE [{database}];";
            create.Parameters.AddWithValue("@Name", database);
            await create.ExecuteNonQueryAsync(ct);
            await using var isolation = connection.CreateCommand();
            isolation.CommandText = $"ALTER DATABASE [{database}] SET ALLOW_SNAPSHOT_ISOLATION ON;";
            await isolation.ExecuteNonQueryAsync(ct);
        }
        var options = new DbContextOptionsBuilder<LedgerDbContext>().UseSqlServer(target.ConnectionString).Options;
        await using (var db = new LedgerDbContext(options))
            await db.Database.MigrateAsync(ct);
        await using var targetConnection = new SqlConnection(target.ConnectionString);
        await targetConnection.OpenAsync(ct);
        await using var provision = targetConnection.CreateCommand();
        // SQL login DDL requires a literal password. Escape it; never log this command text.
        var escapedPassword = runtimePassword.Replace("'", "''", StringComparison.Ordinal);
        provision.CommandText = $"""
            IF SUSER_ID('novawallet_api') IS NULL
                CREATE LOGIN [novawallet_api] WITH PASSWORD = '{escapedPassword}';
            ELSE ALTER LOGIN [novawallet_api] WITH PASSWORD = '{escapedPassword}';
            IF USER_ID('novawallet_api') IS NULL CREATE USER [novawallet_api] FOR LOGIN [novawallet_api];
            GRANT SELECT, INSERT, UPDATE ON dbo.Wallets TO [novawallet_api];
            DENY DELETE ON dbo.Wallets TO [novawallet_api];
            GRANT SELECT, INSERT, UPDATE ON dbo.IdempotencyRecords TO [novawallet_api];
            DENY DELETE ON dbo.IdempotencyRecords TO [novawallet_api];
            GRANT SELECT, INSERT ON dbo.Transfers TO [novawallet_api];
            GRANT SELECT, INSERT ON dbo.WalletTransactions TO [novawallet_api];
            GRANT SELECT, INSERT ON dbo.AuditEntries TO [novawallet_api];
            DENY UPDATE, DELETE ON dbo.Transfers TO [novawallet_api];
            DENY UPDATE, DELETE ON dbo.WalletTransactions TO [novawallet_api];
            DENY UPDATE, DELETE ON dbo.AuditEntries TO [novawallet_api];
            GRANT SELECT ON dbo.__EFMigrationsHistory TO [novawallet_api];
            """;
        await provision.ExecuteNonQueryAsync(ct);
    }

    public static string RuntimeConnection(string privilegedConnection, string runtimePassword)
    {
        var builder = new SqlConnectionStringBuilder(privilegedConnection)
        {
            UserID = "novawallet_api",
            Password = runtimePassword,
            IntegratedSecurity = false
        };
        return builder.ConnectionString;
    }
}
