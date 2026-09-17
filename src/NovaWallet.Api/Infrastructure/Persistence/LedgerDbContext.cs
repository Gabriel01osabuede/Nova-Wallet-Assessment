using Microsoft.EntityFrameworkCore;
using NovaWallet.Api.Domain;
using NovaWallet.Api.Domain.LedgerRecords;

namespace NovaWallet.Api.Infrastructure.Persistence;

public sealed class LedgerDbContext(DbContextOptions<LedgerDbContext> options) : DbContext(options)
{
    public DbSet<Wallet> Wallets => Set<Wallet>();
    public DbSet<Transfer> Transfers => Set<Transfer>();
    public DbSet<WalletTransaction> WalletTransactions => Set<WalletTransaction>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        var wallet = model.Entity<Wallet>();
        wallet.ToTable("Wallets", t =>
        {
            t.HasCheckConstraint("CK_Wallet_Balance", "BalanceKobo >= 0");
            t.HasCheckConstraint("CK_Wallet_Currency", "Currency = 'NGN'");
        });
        wallet.Property(w => w.Id).ValueGeneratedNever();
        wallet.HasIndex(w => w.CustomerId).IsUnique();
        wallet.Property(w => w.Currency).HasColumnType("char(3)").IsRequired();

        var transfer = model.Entity<Transfer>();
        transfer.ToTable("Transfers", t =>
        {
            t.UseSqlOutputClause(false);
            t.HasCheckConstraint("CK_Transfer_Amount", "AmountKobo > 0");
            t.HasCheckConstraint("CK_Transfer_Wallets", "SourceWalletId <> DestinationWalletId");
            t.HasCheckConstraint("CK_Transfer_Status", "Status = 1");
        });
        transfer.Property(t => t.Id).ValueGeneratedNever();
        transfer.HasOne<Wallet>().WithMany().HasForeignKey(t => t.SourceWalletId).OnDelete(DeleteBehavior.Restrict);
        transfer.HasOne<Wallet>().WithMany().HasForeignKey(t => t.DestinationWalletId).OnDelete(DeleteBehavior.Restrict);
        transfer.HasIndex(t => new { t.SourceWalletId, t.CompletedAtUtc }).IncludeProperties(t => new { t.AmountKobo, t.Status });

        var history = model.Entity<WalletTransaction>();
        history.ToTable("WalletTransactions", t =>
        {
            t.UseSqlOutputClause(false);
            t.HasCheckConstraint("CK_History_Amounts", "AmountKobo > 0 AND BalanceBeforeKobo >= 0 AND BalanceAfterKobo >= 0");
            t.HasCheckConstraint("CK_History_Arithmetic", "(Type = 2 AND BalanceBeforeKobo >= AmountKobo AND BalanceAfterKobo = BalanceBeforeKobo - AmountKobo) OR (Type IN (1,3) AND BalanceAfterKobo >= AmountKobo AND BalanceBeforeKobo = BalanceAfterKobo - AmountKobo)");
            t.HasCheckConstraint("CK_History_Reference", "(Type = 1 AND TransferId IS NULL) OR (Type IN (2,3) AND TransferId IS NOT NULL AND TransferId = OperationId)");
        });
        history.HasOne<Wallet>().WithMany().HasForeignKey(t => t.WalletId).OnDelete(DeleteBehavior.Restrict);
        history.HasOne<Transfer>().WithMany().HasForeignKey(t => t.TransferId).OnDelete(DeleteBehavior.Restrict);
        history.HasIndex(t => new { t.WalletId, t.OperationId }).IsUnique();
        history.HasIndex(t => new { t.WalletId, t.CreatedAtUtc, t.Id }).IsDescending(false, true, true);

        var audit = model.Entity<AuditEntry>();
        audit.ToTable("AuditEntries", t =>
        {
            t.UseSqlOutputClause(false);
            t.HasCheckConstraint("CK_Audit_Amounts", "AmountKobo > 0 AND BalanceBeforeKobo >= 0 AND BalanceAfterKobo >= 0");
            t.HasCheckConstraint("CK_Audit_Arithmetic", "(Operation = 2 AND BalanceBeforeKobo >= AmountKobo AND BalanceAfterKobo = BalanceBeforeKobo - AmountKobo) OR (Operation IN (1,3) AND BalanceAfterKobo >= AmountKobo AND BalanceBeforeKobo = BalanceAfterKobo - AmountKobo)");
        });
        audit.HasOne<Wallet>().WithMany().HasForeignKey(t => t.WalletId).OnDelete(DeleteBehavior.Restrict);
        audit.Property(t => t.TraceId).HasColumnType("varchar(32)").IsRequired();
        audit.HasIndex(t => new { t.WalletId, t.OperationId }).IsUnique();
        audit.HasIndex(t => new { t.WalletId, t.OccurredAtUtc, t.Id }).IsDescending(false, true, true);

        var idem = model.Entity<IdempotencyRecord>();
        idem.ToTable("IdempotencyRecords", t => t.HasCheckConstraint("CK_Idempotency_Outcome",
            "(Status = 1 AND HttpStatusCode IS NULL AND CompletedAtUtc IS NULL AND OperationId IS NULL AND ResponseJson IS NULL) OR (Status IN (2,3) AND HttpStatusCode IS NOT NULL AND ResponseJson IS NOT NULL AND ResponseContentType IS NOT NULL AND CompletedAtUtc IS NOT NULL AND ((Status = 2 AND OperationId IS NOT NULL AND HttpStatusCode = 200) OR (Status = 3 AND OperationId IS NULL AND HttpStatusCode = 422)))"));
        idem.Property(t => t.Id).ValueGeneratedNever();
        idem.Property(t => t.Scope).HasColumnType("varchar(80)").UseCollation("Latin1_General_100_BIN2").IsRequired();
        idem.Property(t => t.Key).HasColumnType("varchar(128)").UseCollation("Latin1_General_100_BIN2").IsRequired();
        idem.Property(t => t.RequestHash).HasColumnType("binary(32)").IsRequired();
        idem.Property(t => t.ResponseContentType).HasColumnType("varchar(64)");
        idem.Property(t => t.ResponseLocation).HasColumnType("varchar(256)");
        idem.HasIndex(t => new { t.Scope, t.Key }).IsUnique();
    }

    private void GuardHistory()
    {
        foreach (var entry in ChangeTracker.Entries())
            if ((entry.Entity is Transfer or WalletTransaction or AuditEntry) &&
                (entry.State is EntityState.Modified or EntityState.Deleted))
                throw new InvalidOperationException("Financial history is append-only.");
    }
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        GuardHistory();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        GuardHistory();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }
}
