using NovaWallet.Api.Domain.Enums;

namespace NovaWallet.Api.Domain.LedgerRecords;

public sealed class AuditEntry
{
    public long Id { get; private set; }
    public Guid WalletId { get; init; }
    public Guid OperationId { get; init; }
    public AuditOperation Operation { get; init; }
    public long AmountKobo { get; init; }
    public long BalanceBeforeKobo { get; init; }
    public long BalanceAfterKobo { get; init; }
    public Guid ActorSubjectId { get; init; }
    public DateTimeOffset OccurredAtUtc { get; init; }
    public string TraceId { get; init; } = "";
}
