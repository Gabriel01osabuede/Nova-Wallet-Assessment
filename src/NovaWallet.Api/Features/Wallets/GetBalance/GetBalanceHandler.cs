using Microsoft.EntityFrameworkCore;
using NovaWallet.Api.Common.Errors;
using NovaWallet.Api.Common.Money;
using NovaWallet.Api.Infrastructure.Authentication;
using NovaWallet.Api.Infrastructure.Persistence;

namespace NovaWallet.Api.Features.Wallets.GetBalance;

public sealed record BalanceResponse(Guid WalletId, string Currency, decimal Balance, long BalanceInKobo);
public sealed class GetBalanceHandler(IDbContextFactory<LedgerDbContext> factory, CurrentCaller caller)
{
    public async Task<ApiResult> HandleAsync(Guid walletId, HttpContext http, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var wallet = await db.Wallets.AsNoTracking().Where(w => w.Id == walletId)
            .Select(w => new { w.Id, w.CustomerId, w.Currency, w.BalanceKobo }).SingleOrDefaultAsync(ct);
        if (wallet is null) return ApiErrors.Create(http, 404, "wallet-not-found", "Wallet does not exist.");
        if (wallet.CustomerId != caller.CustomerId)
            return ApiErrors.Create(http, 403, "forbidden", "This wallet does not belong to you.");
        return ApiResult.Ok(new BalanceResponse(wallet.Id, wallet.Currency,
            MoneyConversion.KoboToNaira(wallet.BalanceKobo), wallet.BalanceKobo));
    }
}
