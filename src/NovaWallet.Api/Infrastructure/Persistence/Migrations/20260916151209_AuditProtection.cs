using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NovaWallet.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AuditProtection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var table in new[] { "AuditEntries", "WalletTransactions", "Transfers" })
                migrationBuilder.Sql($"""
                    CREATE TRIGGER dbo.TR_{table}_Immutable ON dbo.{table}
                    AFTER UPDATE, DELETE
                    AS BEGIN
                        SET NOCOUNT ON;
                        THROW 50001, 'Financial history is append-only.', 1;
                    END;
                    """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var table in new[] { "AuditEntries", "WalletTransactions", "Transfers" })
                migrationBuilder.Sql($"DROP TRIGGER IF EXISTS dbo.TR_{table}_Immutable;");
        }
    }
}
