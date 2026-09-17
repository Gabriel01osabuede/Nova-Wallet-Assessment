using NovaWallet.Api.Domain.Enums;

namespace NovaWallet.Api.Domain.LedgerRecords;

public sealed class WalletTransaction
{
    public long Id { get; private set; }
    public Guid WalletId { get; init; }
    public Guid OperationId { get; init; }
    public Guid? TransferId { get; init; }
    public WalletTransactionType Type { get; init; }
    public long AmountKobo { get; init; }
    public long BalanceBeforeKobo { get; init; }
    public long BalanceAfterKobo { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }
}
