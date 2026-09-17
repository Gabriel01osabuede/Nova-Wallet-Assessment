namespace NovaWallet.Api.Domain;

public sealed class Wallet
{
    private Wallet() { }
    public Guid Id { get; private set; }
    public Guid CustomerId { get; private set; }
    public string Currency { get; private set; } = "NGN";
    public long BalanceKobo { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static Wallet Create(Guid customerId, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        CustomerId = customerId,
        CreatedAtUtc = now.ToUniversalTime(),
        UpdatedAtUtc = now.ToUniversalTime()
    };
    public bool CanCredit(long amount) => amount > 0 && amount <= long.MaxValue - BalanceKobo;
    public bool CanDebit(long amount) => amount > 0 && amount <= BalanceKobo;
    public bool TryCredit(long amount, DateTimeOffset now)
    {
        if (!CanCredit(amount)) return false;
        BalanceKobo = checked(BalanceKobo + amount);
        UpdatedAtUtc = now.ToUniversalTime();
        return true;
    }
    public bool TryDebit(long amount, DateTimeOffset now)
    {
        if (!CanDebit(amount)) return false;
        BalanceKobo = checked(BalanceKobo - amount);
        UpdatedAtUtc = now.ToUniversalTime();
        return true;
    }
}
