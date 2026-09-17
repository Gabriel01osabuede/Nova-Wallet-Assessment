using NovaWallet.Api.Domain;
using NovaWallet.Api.Domain.Enums;
using NovaWallet.Api.Domain.LedgerRecords;

namespace NovaWallet.Api.Infrastructure.Persistence;

public static class MutationEvidence
{
    public static void Add(LedgerDbContext db, Wallet wallet, Guid operationId, Guid? transferId,
        WalletTransactionType type, long amount, long before, Guid actor, DateTimeOffset now, string traceId)
    {
        db.WalletTransactions.Add(new WalletTransaction
        {
            WalletId = wallet.Id,
            OperationId = operationId,
            TransferId = transferId,
            Type = type,
            AmountKobo = amount,
            BalanceBeforeKobo = before,
            BalanceAfterKobo = wallet.BalanceKobo,
            CreatedAtUtc = now
        });
        db.AuditEntries.Add(new AuditEntry
        {
            WalletId = wallet.Id,
            OperationId = operationId,
            Operation = (AuditOperation)(int)type,
            AmountKobo = amount,
            BalanceBeforeKobo = before,
            BalanceAfterKobo = wallet.BalanceKobo,
            ActorSubjectId = actor,
            OccurredAtUtc = now,
            TraceId = traceId
        });
    }
}
