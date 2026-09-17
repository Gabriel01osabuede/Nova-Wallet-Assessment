using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using NovaWallet.Api.Common.Money;
using NovaWallet.Api.Common.Errors;
using NovaWallet.Api.Domain;
using NovaWallet.Api.Domain.Enums;
using NovaWallet.Api.Infrastructure.Authentication;
using NovaWallet.Api.Infrastructure.Persistence;

namespace NovaWallet.Api.Features.Wallets.CreditWallet;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreditWalletRequest(NairaAmount Amount, [Required] string Reference);
public sealed record CreditWalletResponse(Guid CreditId, Guid WalletId, long AmountKobo, string Currency,
    [property: JsonPropertyName("totalbalanceInNaira")] decimal TotalBalanceInNaira, DateTimeOffset CompletedAtUtc);

public sealed class CreditWalletHandler(FinancialTransaction transaction, CurrentCaller caller, TimeProvider clock)
{
    public Task<ApiResult> HandleAsync(Guid walletId, CreditWalletRequest request, HttpContext http, CancellationToken ct)
    {
        if (walletId == Guid.Empty || request.Amount.Kobo <= 0 || !IdempotencyFingerprint.ValidKey(request.Reference))
            return Task.FromResult(ApiErrors.Create(http, 400, "invalid-request", "Supply a wallet, positive naira amount and valid reference."));
        var actor = caller.SubjectId;
        return transaction.ExecuteAsync("credit:nip-simulator", request.Reference,
            IdempotencyFingerprint.Credit(walletId, request.Amount.Kobo), http, async (db, token) =>
        {
            var wallet = await FinancialTransaction.LockWalletAsync(db, walletId, token);
            if (wallet is null) return new(ApiErrors.Create(http, 404, "wallet-not-found", "Wallet does not exist."));
            var now = clock.GetUtcNow();
            var before = wallet.BalanceKobo;
            if (!wallet.TryCredit(request.Amount.Kobo, now))
                return new(ApiErrors.Create(http, 422, "balance-capacity-exceeded", "The credit exceeds wallet capacity."));
            var id = Guid.NewGuid();
            MutationEvidence.Add(db, wallet, id, null, WalletTransactionType.InboundCredit,
                request.Amount.Kobo, before, actor, now, ApiErrors.TraceId(http));
            return new(ApiResult.Ok(new CreditWalletResponse(id, wallet.Id, request.Amount.Kobo,
                wallet.Currency, MoneyConversion.KoboToNaira(wallet.BalanceKobo), now)), id);
        }, ct);
    }
}
