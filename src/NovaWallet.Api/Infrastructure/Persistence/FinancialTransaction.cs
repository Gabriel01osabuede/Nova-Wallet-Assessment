using System.Data;
using System.Security.Cryptography;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using NovaWallet.Api.Common.Errors;
using NovaWallet.Api.Domain;
using NovaWallet.Api.Domain.Enums;
using NovaWallet.Api.Domain.LedgerRecords;

namespace NovaWallet.Api.Infrastructure.Persistence;

public sealed record MutationDecision(ApiResult Result, Guid? OperationId = null);

public sealed class FinancialTransaction(
    IDbContextFactory<LedgerDbContext> factory, TimeProvider clock, ILogger<FinancialTransaction> logger)
{
    private sealed class ApplicationLockDeadlockException : Exception;

    public async Task<ApiResult> ExecuteAsync(string scope, string key, byte[] hash, HttpContext http,
        Func<LedgerDbContext, CancellationToken, Task<MutationDecision>> mutate, CancellationToken ct)
    {
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                await using var db = await factory.CreateDbContextAsync(ct);
                await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
                await db.Database.ExecuteSqlRawAsync("SET LOCK_TIMEOUT 10000;", ct);
                var lockResult = await AcquireKeyLockAsync(db, scope, key, ct);
                if (lockResult == -3) throw new ApplicationLockDeadlockException();
                if (lockResult == -2) ct.ThrowIfCancellationRequested();
                if (lockResult == -1 || lockResult == -2)
                    return ApiErrors.Create(http, 503, "operation-busy", "Retry this operation using the same key.");
                if (lockResult < 0) throw new InvalidOperationException("Idempotency lock configuration failed.");

                var existing = await db.IdempotencyRecords.SingleOrDefaultAsync(r => r.Scope == scope && r.Key == key, ct);
                if (existing is not null)
                {
                    if (!CryptographicOperations.FixedTimeEquals(existing.RequestHash, hash))
                        return ApiErrors.Create(http, 409, "idempotency-conflict", "This key is already bound to a different request.");
                    if (existing.Status == IdempotencyStatus.Processing || existing.ResponseJson is null)
                        throw new InvalidOperationException("A non-terminal idempotency record was committed.");
                    await transaction.CommitAsync(ct);
                    logger.LogInformation("Financial outcome replayed {OperationId} {TraceId}", existing.OperationId, ApiErrors.TraceId(http));
                    return new ApiResult(existing.HttpStatusCode!.Value, new { }, true, existing.ResponseLocation,
                        existing.ResponseJson, existing.ResponseContentType!);
                }

                var record = new IdempotencyRecord
                {
                    Scope = scope,
                    Key = key,
                    RequestHash = hash,
                    Status = IdempotencyStatus.Processing,
                    CreatedAtUtc = clock.GetUtcNow()
                };
                db.IdempotencyRecords.Add(record);
                var decision = await mutate(db, ct);
                var result = decision.Result;
                if (result.Status != 200 && result.Status != 422)
                    return result; // Dispose rolls back: auth/existence failures do not reserve a key.

                record.Status = result.Status == 200 ? IdempotencyStatus.Completed : IdempotencyStatus.Rejected;
                record.OperationId = decision.OperationId;
                record.HttpStatusCode = result.Status;
                record.ResponseJson = result.Json;
                record.ResponseContentType = result.ContentType;
                record.ResponseLocation = result.Location;
                record.CompletedAtUtc = clock.GetUtcNow();
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                logger.LogInformation("Financial outcome committed {OperationId} {Status} {TraceId}",
                    decision.OperationId, result.Status, ApiErrors.TraceId(http));
                return result;
            }
            catch (Exception exception) when (GetSqlException(exception)?.Number == 1222)
            {
                return ApiErrors.Create(http, 503, "operation-busy", "Retry this operation using the same key.");
            }
            catch (Exception exception) when (exception is ApplicationLockDeadlockException ||
                GetSqlException(exception) is { } sql && IsTransient(sql))
            {
                logger.LogWarning("Transient financial operation failure {Attempt} {TraceId}", attempt, ApiErrors.TraceId(http));
                if (attempt == 3)
                    return ApiErrors.Create(http, 503, "datastore-unavailable",
                        "The outcome may require verification. Retry using the same key.");
                // A fresh attempt first reacquires the key lock and checks committed state.
                // This is also safe when the preceding commit succeeded but its acknowledgement was lost.
                await Task.Delay(TimeSpan.FromMilliseconds(100 * attempt + Random.Shared.Next(25, 100)), ct);
            }
        }
        throw new InvalidOperationException("Unreachable retry state.");
    }

    private static bool IsTransient(SqlException exception) => exception.Errors.Cast<SqlError>().Any(e =>
        e.Number is -2 or 1205 or 64 or 233 or 10053 or 10054 or 10060 or 10928 or 10929 or 40197 or 40501 or 40613 or 49918 or 49919 or 49920);

    private static SqlException? GetSqlException(Exception exception) => exception switch
    {
        SqlException sql => sql,
        DbUpdateException { InnerException: SqlException sql } => sql,
        _ => null
    };

    private static async Task<int> AcquireKeyLockAsync(LedgerDbContext db, string scope, string key, CancellationToken ct)
    {
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.Transaction = db.Database.CurrentTransaction!.GetDbTransaction();
        command.CommandText = """
            DECLARE @Result int;
            EXEC @Result = sys.sp_getapplock @Resource = @Resource, @LockMode = 'Exclusive',
                @LockOwner = 'Transaction', @LockTimeout = 10000, @DbPrincipal = 'public';
            SELECT @Result;
            """;
        command.Parameters.Add(new SqlParameter("@Resource", SqlDbType.NVarChar, 255)
        { Value = IdempotencyFingerprint.Resource(scope, key) });
        return Convert.ToInt32(await command.ExecuteScalarAsync(ct));
    }

    public static async Task<Wallet?> LockWalletAsync(LedgerDbContext db, Guid id, CancellationToken ct) =>
        await db.Wallets.FromSqlInterpolated($"""
            SELECT Id, CustomerId, Currency, BalanceKobo, CreatedAtUtc, UpdatedAtUtc
            FROM dbo.Wallets WITH (UPDLOCK, HOLDLOCK) WHERE Id = {id}
            """).SingleOrDefaultAsync(ct);
}
