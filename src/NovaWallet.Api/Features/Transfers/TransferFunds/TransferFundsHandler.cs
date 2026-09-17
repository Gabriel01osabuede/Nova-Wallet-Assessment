using System.Text.Json.Serialization;
using NovaWallet.Api.Common.Money;
using Microsoft.EntityFrameworkCore;
using NovaWallet.Api.Common.Errors;
using NovaWallet.Api.Domain;
using NovaWallet.Api.Domain.Enums;
using NovaWallet.Api.Domain.LedgerRecords;
using NovaWallet.Api.Infrastructure.Authentication;
using NovaWallet.Api.Infrastructure.Persistence;
using NovaWallet.Api.Infrastructure.Time;

namespace NovaWallet.Api.Features.Transfers.TransferFunds;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TransferFundsRequest(Guid SourceWalletId, Guid DestinationWalletId, NairaAmount Amount);
public sealed record TransferFundsResponse(Guid TransferId, Guid SourceWalletId, Guid DestinationWalletId,
    long AmountKobo, string Currency, TransferStatus Status, decimal SourceBalanceInNaira, DateTimeOffset CompletedAtUtc);

public sealed class TransferFundsHandler(IDbContextFactory<LedgerDbContext> factory, FinancialTransaction transaction,
    CurrentCaller caller, TimeProvider clock, IConfiguration config)
{
    public async Task<ApiResult> HandleAsync(TransferFundsRequest request, string? key, HttpContext http, CancellationToken ct)
    {
        if (request.SourceWalletId == Guid.Empty || request.DestinationWalletId == Guid.Empty ||
            request.SourceWalletId == request.DestinationWalletId || request.Amount.Kobo <= 0 ||
            !IdempotencyFingerprint.ValidKey(key))
            return ApiErrors.Create(http, 400, "invalid-request", "Supply distinct wallets, a positive naira amount and a valid Idempotency-Key.");
        var customer = caller.CustomerId;
        var actor = caller.SubjectId;
        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            var owner = await db.Wallets.Where(w => w.Id == request.SourceWalletId).Select(w => (Guid?)w.CustomerId).SingleOrDefaultAsync(ct);
            if (owner is null) return ApiErrors.Create(http, 404, "wallet-not-found", "Source wallet does not exist.");
            if (owner != customer) return ApiErrors.Create(http, 403, "forbidden", "The source wallet does not belong to you.");
        }
        return await transaction.ExecuteAsync($"transfer:{customer:D}", key!,
            IdempotencyFingerprint.Transfer(request.SourceWalletId, request.DestinationWalletId, request.Amount.Kobo),
            http, async (db, token) =>
        {
            var wallets = new Dictionary<Guid, Wallet>();
            foreach (var walletId in new[] { request.SourceWalletId, request.DestinationWalletId }
                .OrderBy(id => id.ToString("D"), StringComparer.Ordinal))
            {
                var wallet = await FinancialTransaction.LockWalletAsync(db, walletId, token);
                if (wallet is null) return new(ApiErrors.Create(http, 404, "wallet-not-found", "A required wallet does not exist."));
                wallets.Add(walletId, wallet);
            }
            var source = wallets[request.SourceWalletId];
            var destination = wallets[request.DestinationWalletId];
            if (source.CustomerId != customer) return new(ApiErrors.Create(http, 403, "forbidden", "The source wallet does not belong to you."));
            if (!source.CanDebit(request.Amount.Kobo))
                return new(ApiErrors.Create(http, 422, "insufficient-funds", "The source wallet has insufficient funds for this transfer."));
            if (!destination.CanCredit(request.Amount.Kobo))
                return new(ApiErrors.Create(http, 422, "balance-capacity-exceeded", "The credit exceeds destination capacity."));
            var now = clock.GetUtcNow();
            var (start, end) = WatDay.Window(now);
            var total = await db.Database.SqlQuery<decimal>($"""
                SELECT COALESCE(SUM(CAST(AmountKobo AS decimal(38,0))), 0) AS Value
                FROM dbo.Transfers WHERE SourceWalletId = {source.Id} AND Status = 1
                    AND CompletedAtUtc >= {start} AND CompletedAtUtc < {end}
                """).SingleAsync(token);
            var limit = config.GetValue<long>("Ledger:DailyLimitKobo", 50_000_000);
            if (total < 0 || total > limit) throw new InvalidOperationException("Daily ledger integrity check failed.");
            if (!WatDay.WithinLimit(checked((long)total), request.Amount.Kobo, limit))
                return new(ApiErrors.Create(http, 422, "daily-limit-exceeded", "The transfer exceeds the wallet's daily outbound limit."));
            var id = Guid.NewGuid();
            var sourceBefore = source.BalanceKobo;
            var destinationBefore = destination.BalanceKobo;
            if (!source.TryDebit(request.Amount.Kobo, now) || !destination.TryCredit(request.Amount.Kobo, now))
                throw new InvalidOperationException("Locked mutation violated a previously checked invariant.");
            db.Transfers.Add(new Transfer
            {
                Id = id,
                SourceWalletId = source.Id,
                DestinationWalletId = destination.Id,
                AmountKobo = request.Amount.Kobo,
                CompletedAtUtc = now,
                InitiatedBySubjectId = actor
            });
            MutationEvidence.Add(db, source, id, id, WalletTransactionType.TransferDebit,
                request.Amount.Kobo, sourceBefore, actor, now, ApiErrors.TraceId(http));
            MutationEvidence.Add(db, destination, id, id, WalletTransactionType.TransferCredit,
                request.Amount.Kobo, destinationBefore, actor, now, ApiErrors.TraceId(http));
            return new(ApiResult.Ok(new TransferFundsResponse(id, source.Id, destination.Id, request.Amount.Kobo,
                "NGN", TransferStatus.Completed, MoneyConversion.KoboToNaira(source.BalanceKobo), now)), id);
        }, ct);
    }
}
