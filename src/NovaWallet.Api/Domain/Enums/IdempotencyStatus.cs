namespace NovaWallet.Api.Domain.Enums;

public enum IdempotencyStatus
{
    Processing = 1,
    Completed = 2,
    Rejected = 3
}
