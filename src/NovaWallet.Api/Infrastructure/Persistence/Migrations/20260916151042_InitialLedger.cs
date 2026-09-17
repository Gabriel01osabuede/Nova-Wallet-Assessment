using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NovaWallet.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IdempotencyRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Scope = table.Column<string>(type: "varchar(80)", nullable: false, collation: "Latin1_General_100_BIN2"),
                    Key = table.Column<string>(type: "varchar(128)", nullable: false, collation: "Latin1_General_100_BIN2"),
                    RequestHash = table.Column<byte[]>(type: "binary(32)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    OperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    HttpStatusCode = table.Column<int>(type: "int", nullable: true),
                    ResponseJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ResponseContentType = table.Column<string>(type: "varchar(64)", nullable: true),
                    ResponseLocation = table.Column<string>(type: "varchar(256)", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdempotencyRecords", x => x.Id);
                    table.CheckConstraint("CK_Idempotency_Outcome", "(Status = 1 AND HttpStatusCode IS NULL AND CompletedAtUtc IS NULL AND OperationId IS NULL AND ResponseJson IS NULL) OR (Status IN (2,3) AND HttpStatusCode IS NOT NULL AND ResponseJson IS NOT NULL AND ResponseContentType IS NOT NULL AND CompletedAtUtc IS NOT NULL AND ((Status = 2 AND OperationId IS NOT NULL AND HttpStatusCode = 200) OR (Status = 3 AND OperationId IS NULL AND HttpStatusCode = 422)))");
                });

            migrationBuilder.CreateTable(
                name: "Wallets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Currency = table.Column<string>(type: "char(3)", nullable: false),
                    BalanceKobo = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Wallets", x => x.Id);
                    table.CheckConstraint("CK_Wallet_Balance", "BalanceKobo >= 0");
                    table.CheckConstraint("CK_Wallet_Currency", "Currency = 'NGN'");
                });

            migrationBuilder.CreateTable(
                name: "AuditEntries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WalletId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Operation = table.Column<int>(type: "int", nullable: false),
                    AmountKobo = table.Column<long>(type: "bigint", nullable: false),
                    BalanceBeforeKobo = table.Column<long>(type: "bigint", nullable: false),
                    BalanceAfterKobo = table.Column<long>(type: "bigint", nullable: false),
                    ActorSubjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    TraceId = table.Column<string>(type: "varchar(32)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEntries", x => x.Id);
                    table.CheckConstraint("CK_Audit_Amounts", "AmountKobo > 0 AND BalanceBeforeKobo >= 0 AND BalanceAfterKobo >= 0");
                    table.CheckConstraint("CK_Audit_Arithmetic", "(Operation = 2 AND BalanceBeforeKobo >= AmountKobo AND BalanceAfterKobo = BalanceBeforeKobo - AmountKobo) OR (Operation IN (1,3) AND BalanceAfterKobo >= AmountKobo AND BalanceBeforeKobo = BalanceAfterKobo - AmountKobo)");
                    table.ForeignKey(
                        name: "FK_AuditEntries_Wallets_WalletId",
                        column: x => x.WalletId,
                        principalTable: "Wallets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Transfers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceWalletId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DestinationWalletId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AmountKobo = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    InitiatedBySubjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Transfers", x => x.Id);
                    table.CheckConstraint("CK_Transfer_Amount", "AmountKobo > 0");
                    table.CheckConstraint("CK_Transfer_Status", "Status = 1");
                    table.CheckConstraint("CK_Transfer_Wallets", "SourceWalletId <> DestinationWalletId");
                    table.ForeignKey(
                        name: "FK_Transfers_Wallets_DestinationWalletId",
                        column: x => x.DestinationWalletId,
                        principalTable: "Wallets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Transfers_Wallets_SourceWalletId",
                        column: x => x.SourceWalletId,
                        principalTable: "Wallets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WalletTransactions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WalletId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TransferId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Type = table.Column<int>(type: "int", nullable: false),
                    AmountKobo = table.Column<long>(type: "bigint", nullable: false),
                    BalanceBeforeKobo = table.Column<long>(type: "bigint", nullable: false),
                    BalanceAfterKobo = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WalletTransactions", x => x.Id);
                    table.CheckConstraint("CK_History_Amounts", "AmountKobo > 0 AND BalanceBeforeKobo >= 0 AND BalanceAfterKobo >= 0");
                    table.CheckConstraint("CK_History_Arithmetic", "(Type = 2 AND BalanceBeforeKobo >= AmountKobo AND BalanceAfterKobo = BalanceBeforeKobo - AmountKobo) OR (Type IN (1,3) AND BalanceAfterKobo >= AmountKobo AND BalanceBeforeKobo = BalanceAfterKobo - AmountKobo)");
                    table.CheckConstraint("CK_History_Reference", "(Type = 1 AND TransferId IS NULL) OR (Type IN (2,3) AND TransferId IS NOT NULL AND TransferId = OperationId)");
                    table.ForeignKey(
                        name: "FK_WalletTransactions_Transfers_TransferId",
                        column: x => x.TransferId,
                        principalTable: "Transfers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WalletTransactions_Wallets_WalletId",
                        column: x => x.WalletId,
                        principalTable: "Wallets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_WalletId_OccurredAtUtc_Id",
                table: "AuditEntries",
                columns: new[] { "WalletId", "OccurredAtUtc", "Id" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_WalletId_OperationId",
                table: "AuditEntries",
                columns: new[] { "WalletId", "OperationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyRecords_Scope_Key",
                table: "IdempotencyRecords",
                columns: new[] { "Scope", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Transfers_DestinationWalletId",
                table: "Transfers",
                column: "DestinationWalletId");

            migrationBuilder.CreateIndex(
                name: "IX_Transfers_SourceWalletId_CompletedAtUtc",
                table: "Transfers",
                columns: new[] { "SourceWalletId", "CompletedAtUtc" })
                .Annotation("SqlServer:Include", new[] { "AmountKobo", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Wallets_CustomerId",
                table: "Wallets",
                column: "CustomerId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WalletTransactions_TransferId",
                table: "WalletTransactions",
                column: "TransferId");

            migrationBuilder.CreateIndex(
                name: "IX_WalletTransactions_WalletId_CreatedAtUtc_Id",
                table: "WalletTransactions",
                columns: new[] { "WalletId", "CreatedAtUtc", "Id" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "IX_WalletTransactions_WalletId_OperationId",
                table: "WalletTransactions",
                columns: new[] { "WalletId", "OperationId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditEntries");

            migrationBuilder.DropTable(
                name: "IdempotencyRecords");

            migrationBuilder.DropTable(
                name: "WalletTransactions");

            migrationBuilder.DropTable(
                name: "Transfers");

            migrationBuilder.DropTable(
                name: "Wallets");
        }
    }
}
