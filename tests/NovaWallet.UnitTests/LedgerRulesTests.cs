using NovaWallet.Api.Common;
using NovaWallet.Api.Domain;
using NovaWallet.Api.Infrastructure.Persistence;
using NovaWallet.Api.Infrastructure.Time;
using Xunit;

namespace NovaWallet.UnitTests;

public sealed class LedgerRulesTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-16T10:00:00Z");

    [Fact]
    public void NewWalletIsZeroNgn()
    {
        var customer = Guid.NewGuid();
        var wallet = Wallet.Create(customer, Now);
        Assert.Equal(customer, wallet.CustomerId);
        Assert.NotEqual(Guid.Empty, wallet.Id);
        Assert.Equal("NGN", wallet.Currency);
        Assert.Equal(0, wallet.BalanceKobo);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(long.MinValue)]
    public void InvalidAmountsDoNotMutate(long amount)
    {
        var wallet = Wallet.Create(Guid.NewGuid(), Now);
        Assert.False(wallet.TryCredit(amount, Now));
        Assert.False(wallet.TryDebit(amount, Now));
        Assert.Equal(0, wallet.BalanceKobo);
    }

    [Fact]
    public void OverflowAndOverspendingAreRejectedWithoutMutation()
    {
        var wallet = Wallet.Create(Guid.NewGuid(), Now);
        Assert.True(wallet.TryCredit(long.MaxValue, Now));
        Assert.False(wallet.TryCredit(1, Now));
        Assert.Equal(long.MaxValue, wallet.BalanceKobo);
        Assert.True(wallet.TryDebit(long.MaxValue, Now));
        Assert.False(wallet.TryDebit(1, Now));
        Assert.Equal(0, wallet.BalanceKobo);
    }

    [Theory]
    [InlineData("2026-09-16T22:59:59Z", "2026-09-15T23:00:00Z")]
    [InlineData("2026-09-16T23:00:00Z", "2026-09-16T23:00:00Z")]
    [InlineData("2026-09-17T00:00:00+01:00", "2026-09-16T23:00:00Z")]
    public void LimitWindowUsesWatMidnight(string timestamp, string expected)
    {
        var window = WatDay.Window(DateTimeOffset.Parse(timestamp));
        Assert.Equal(DateTimeOffset.Parse(expected), window.StartUtc);
        Assert.Equal(TimeSpan.FromDays(1), window.EndUtc - window.StartUtc);
    }

    [Theory]
    [InlineData(45_000_000, 4_000_000, true)]
    [InlineData(49_000_000, 4_000_000, false)]
    [InlineData(50_000_000, 1, false)]
    [InlineData(0, long.MaxValue, false)]
    public void LimitCheckAvoidsOverflow(long used, long amount, bool expected) =>
        Assert.Equal(expected, WatDay.WithinLimit(used, amount, 50_000_000));

    [Theory]
    [InlineData("Transfer-1:_x.y", true)]
    [InlineData("", false)]
    [InlineData(" key", false)]
    [InlineData("key ", false)]
    [InlineData("key/1", false)]
    [InlineData("kéy", false)]
    public void KeysAreStrict(string key, bool expected) => Assert.Equal(expected, IdempotencyFingerprint.ValidKey(key));

    [Fact]
    public void FingerprintsBindWalletsAndAmount()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        Assert.Equal(IdempotencyFingerprint.Transfer(a, b, 100), IdempotencyFingerprint.Transfer(a, b, 100));
        Assert.NotEqual(IdempotencyFingerprint.Transfer(a, b, 100), IdempotencyFingerprint.Transfer(a, b, 101));
        Assert.NotEqual(IdempotencyFingerprint.Transfer(a, b, 100), IdempotencyFingerprint.Transfer(b, a, 100));
        Assert.NotEqual(IdempotencyFingerprint.Resource("transfer:a", "Key"), IdempotencyFingerprint.Resource("transfer:a", "key"));
        Assert.NotEqual(IdempotencyFingerprint.Resource("transfer:a", "key"), IdempotencyFingerprint.Resource("transfer:b", "key"));
        Assert.False(IdempotencyFingerprint.ValidKey(new string('a', 129)));
    }

    [Theory]
    [InlineData(1, 20, true)]
    [InlineData(0, 20, false)]
    [InlineData(1, 101, false)]
    [InlineData(int.MaxValue, 100, false)]
    public void PaginationBoundsAreChecked(int page, int pageSize, bool expected) => Assert.Equal(expected, Pagination.Valid(page, pageSize));
}
