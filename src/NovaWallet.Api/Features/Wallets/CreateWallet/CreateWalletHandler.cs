using System.ComponentModel.DataAnnotations;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using NovaWallet.Api.Common.Errors;
using NovaWallet.Api.Domain;
using NovaWallet.Api.Infrastructure.Authentication;
using NovaWallet.Api.Infrastructure.Persistence;

namespace NovaWallet.Api.Features.Wallets.CreateWallet;

public sealed record CreateWalletRequest([Required] Guid CustomerId);
public sealed record CreateWalletResponse(Guid WalletId, Guid CustomerId, string Currency, long BalanceKobo, DateTimeOffset CreatedAtUtc);

public sealed class CreateWalletHandler(IDbContextFactory<LedgerDbContext> factory, CurrentCaller caller, TimeProvider clock)
{
    public async Task<ApiResult> HandleAsync(CreateWalletRequest request, HttpContext http, CancellationToken ct)
    {
        if (request.CustomerId == Guid.Empty)
            return ApiErrors.Create(http, 400, "invalid-request", "Customer ID must be a non-empty GUID.");
        if (request.CustomerId != caller.CustomerId)
            return ApiErrors.Create(http, 403, "forbidden", "You can create only your own wallet.");
        await using var db = await factory.CreateDbContextAsync(ct);
        var existing = await db.Wallets.AsNoTracking().SingleOrDefaultAsync(w => w.CustomerId == request.CustomerId, ct);
        if (existing is not null) return Duplicate(existing.Id, http);
        var wallet = Wallet.Create(request.CustomerId, clock.GetUtcNow());
        db.Wallets.Add(wallet);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException e) when (e.InnerException is SqlException { Number: 2601 or 2627 })
        {
            await using var recovery = await factory.CreateDbContextAsync(ct);
            var id = await recovery.Wallets.Where(w => w.CustomerId == request.CustomerId).Select(w => w.Id).SingleAsync(ct);
            return Duplicate(id, http);
        }
        return new ApiResult(201, new CreateWalletResponse(wallet.Id, wallet.CustomerId, wallet.Currency, 0, wallet.CreatedAtUtc),
            Location: $"/api/v1/wallets/{wallet.Id}/balance");
    }

    private static ApiResult Duplicate(Guid id, HttpContext http)
    {
        var result = ApiErrors.Create(http, 409, "wallet-already-exists", "A wallet already exists for this customer.");
        ((Microsoft.AspNetCore.Mvc.ProblemDetails)result.Body).Extensions["walletId"] = id;
        return result with { Location = $"/api/v1/wallets/{id}/balance" };
    }
}
