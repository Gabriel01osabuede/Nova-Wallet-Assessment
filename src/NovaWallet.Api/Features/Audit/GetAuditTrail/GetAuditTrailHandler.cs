using System.Data;
using Microsoft.EntityFrameworkCore;
using NovaWallet.Api.Common;
using NovaWallet.Api.Common.Errors;
using NovaWallet.Api.Infrastructure.Persistence;

namespace NovaWallet.Api.Features.Audit.GetAuditTrail;

public sealed class GetAuditTrailHandler(IDbContextFactory<LedgerDbContext> factory)
{
    public async Task<ApiResult> HandleAsync(Guid id, int page, int pageSize, HttpContext http, CancellationToken ct)
    {
        if (!Pagination.Valid(page, pageSize))
            return ApiErrors.Create(http, 400, "invalid-request", "Page must be positive and pageSize between 1 and 100.");
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var snapshot = await db.Database.BeginTransactionAsync(IsolationLevel.Snapshot, ct);
        if (!await db.Wallets.AnyAsync(w => w.Id == id, ct))
            return ApiErrors.Create(http, 404, "wallet-not-found", "Wallet does not exist.");
        var query = db.AuditEntries.AsNoTracking().Where(a => a.WalletId == id);
        var total = await query.LongCountAsync(ct);
        var items = await query.OrderByDescending(a => a.OccurredAtUtc).ThenByDescending(a => a.Id)
            .Skip(Pagination.Offset(page, pageSize)).Take(pageSize)
            .Select(a => new
            {
                a.Id,
                a.OperationId,
                a.Operation,
                a.AmountKobo,
                a.BalanceBeforeKobo,
                a.BalanceAfterKobo,
                a.ActorSubjectId,
                a.OccurredAtUtc,
                a.TraceId
            }).ToListAsync(ct);
        await snapshot.CommitAsync(ct);
        return ApiResult.Ok(new { walletId = id, page, pageSize, totalCount = total, items });
    }
}
