using NovaWallet.Api.Domain.Enums;

namespace NovaWallet.Api.Domain.LedgerRecords;

public sealed class IdempotencyRecord
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Scope { get; init; } = "";
    public string Key { get; init; } = "";
    public byte[] RequestHash { get; init; } = [];
    public IdempotencyStatus Status { get; set; }
    public Guid? OperationId { get; set; }
    public int? HttpStatusCode { get; set; }
    public string? ResponseJson { get; set; }
    public string? ResponseContentType { get; set; }
    public string? ResponseLocation { get; set; }
    public DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
}
