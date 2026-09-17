using System.Data;
using Microsoft.EntityFrameworkCore;
using NovaWallet.Api.Common;
using NovaWallet.Api.Common.Errors;
using NovaWallet.Api.Domain;
using NovaWallet.Api.Domain.Enums;
using NovaWallet.Api.Infrastructure.Authentication;
using NovaWallet.Api.Infrastructure.Persistence;

namespace NovaWallet.Api.Features.Statements.GetStatement;

public sealed record StatementItem(long TransactionId, Guid OperationId, WalletTransactionType Type,
    long AmountKobo, long BalanceBeforeKobo, long BalanceAfterKobo, DateTimeOffset CreatedAtUtc);
public sealed class GetStatementHandler(IDbContextFactory<LedgerDbContext> factory, CurrentCaller caller)
{
    public async Task<ApiResult> HandleAsync(Guid id, int page, int pageSize, HttpContext http, CancellationToken ct)
    {
        if (!Pagination.Valid(page, pageSize))
            return ApiErrors.Create(http, 400, "invalid-request", "Page must be positive and pageSize between 1 and 100.");
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var snapshot = await db.Database.BeginTransactionAsync(IsolationLevel.Snapshot, ct);
        var owner = await db.Wallets.Where(w => w.Id == id).Select(w => (Guid?)w.CustomerId).SingleOrDefaultAsync(ct);
        if (owner is null) return ApiErrors.Create(http, 404, "wallet-not-found", "Wallet does not exist.");
        if (owner != caller.CustomerId) return ApiErrors.Create(http, 403, "forbidden", "This wallet does not belong to you.");
        var query = db.WalletTransactions.AsNoTracking().Where(t => t.WalletId == id);
        var total = await query.LongCountAsync(ct);
        var items = await query.OrderByDescending(t => t.CreatedAtUtc).ThenByDescending(t => t.Id)
            .Skip(Pagination.Offset(page, pageSize)).Take(pageSize)
            .Select(t => new StatementItem(t.Id, t.OperationId, t.Type, t.AmountKobo,
                t.BalanceBeforeKobo, t.BalanceAfterKobo, t.CreatedAtUtc)).ToListAsync(ct);
        await snapshot.CommitAsync(ct);
        return ApiResult.Ok(new { walletId = id, currency = "NGN", page, pageSize, totalCount = total, items });
    }
}
