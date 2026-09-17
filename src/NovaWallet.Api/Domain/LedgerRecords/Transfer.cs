using NovaWallet.Api.Domain.Enums;

namespace NovaWallet.Api.Domain.LedgerRecords;

public sealed class Transfer
{
    public Guid Id { get; init; }
    public Guid SourceWalletId { get; init; }
    public Guid DestinationWalletId { get; init; }
    public long AmountKobo { get; init; }
    public TransferStatus Status { get; init; } = TransferStatus.Completed;
    public DateTimeOffset CompletedAtUtc { get; init; }
    public Guid InitiatedBySubjectId { get; init; }
}
