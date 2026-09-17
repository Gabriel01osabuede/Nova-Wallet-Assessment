using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using NovaWallet.Api.Features.Transfers.TransferFunds;
using NovaWallet.Api.Features.Wallets.CreateWallet;
using NovaWallet.Api.Features.Wallets.CreditWallet;
using NovaWallet.Api.Infrastructure.Authentication;
using Xunit;
using NovaWallet.Api.Common.Money;

namespace NovaWallet.IntegrationTests;

[Collection("SQL")]
public sealed class WalletApiTests(SqlFixture sql)
{
    private static HttpClient Client(LedgerFactory factory, Guid subject, string role = "Customer")
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            DemoTokens.Mint(LedgerFactory.TokenConfiguration, subject, role, DateTimeOffset.UtcNow));
        return client;
    }
    private static async Task<Guid> WalletAsync(HttpClient client, Guid customer)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/wallets", new CreateWalletRequest(customer));
        if (response.StatusCode == HttpStatusCode.InternalServerError)
        {
            var payload = await response.Content.ReadAsStringAsync();
            throw new Xunit.Sdk.XunitException("Wallet request failed: " + payload);
        }
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CreateWalletResponse>();
        Assert.NotNull(body);
        Assert.Equal(0, body.BalanceKobo);
        return body.WalletId;
    }
    private static async Task CreditAsync(HttpClient funding, Guid wallet, long amount, string? reference = null)
    {
        using var response = await funding.PostAsJsonAsync($"/api/v1/wallets/{wallet}/credits",
            new CreditWalletRequest(new NairaAmount(amount), reference ?? Guid.NewGuid().ToString("N")));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
    private static Task<HttpResponseMessage> TransferAsync(HttpClient client, Guid source, Guid destination, long amount, string key)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/transfers")
        { Content = JsonContent.Create(new TransferFundsRequest(source, destination, new NairaAmount(amount))) };
        request.Headers.Add("Idempotency-Key", key);
        return client.SendAsync(request);
    }
    private static async Task<long> BalanceAsync(HttpClient client, Guid wallet)
    {
        using var response = await client.GetAsync($"/api/v1/wallets/{wallet}/balance");
        response.EnsureSuccessStatusCode();
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.False(body.RootElement.TryGetProperty("balanceKobo", out _));
        var kobo = body.RootElement.GetProperty("balanceInKobo").GetInt64();
        Assert.Equal(kobo, checked((long)(body.RootElement.GetProperty("balance").GetDecimal() * 100m)));
        return kobo;
    }

    [Fact]
    public async Task NairaRequestsConvertExactlyAndNormalizeIdempotency()
    {
        var database = await sql.CreateDatabaseAsync();
        using var factory = new LedgerFactory(database);
        var a = Guid.NewGuid(); var b = Guid.NewGuid();
        using var customerA = Client(factory, a);
        using var customerB = Client(factory, b);
        using var funding = Client(factory, Guid.NewGuid(), "FundingSystem");
        var source = await WalletAsync(customerA, a);
        var destination = await WalletAsync(customerB, b);
        using var creditBody = new StringContent("{\"amount\":10000.50,\"reference\":\"naira-credit\"}", Encoding.UTF8, "application/json");
        using var credit = await funding.PostAsync($"/api/v1/wallets/{source}/credits", creditBody);
        Assert.Equal(HttpStatusCode.OK, credit.StatusCode);
        using var creditJson = JsonDocument.Parse(await credit.Content.ReadAsStringAsync());
        Assert.Equal(10000.50m, creditJson.RootElement.GetProperty("totalbalanceInNaira").GetDecimal());
        Assert.Equal(1_000_050, creditJson.RootElement.GetProperty("amountKobo").GetInt64());
        Assert.False(creditJson.RootElement.TryGetProperty("balanceAfterKobo", out _));
        Assert.Equal(1_000_050, await BalanceAsync(customerA, source));
        using var balanceResponse = await customerA.GetAsync($"/api/v1/wallets/{source}/balance");
        Assert.Equal(HttpStatusCode.OK, balanceResponse.StatusCode);
        using var balanceJson = JsonDocument.Parse(await balanceResponse.Content.ReadAsStringAsync());
        Assert.Equal(10000.50m, balanceJson.RootElement.GetProperty("balance").GetDecimal());
        Assert.Equal(1_000_050, balanceJson.RootElement.GetProperty("balanceInKobo").GetInt64());
        Assert.Equal("NGN", balanceJson.RootElement.GetProperty("currency").GetString());
        Assert.False(balanceJson.RootElement.TryGetProperty("balanceKobo", out _));
        using var replayBody = new StringContent("{\"amount\":10000.5,\"reference\":\"naira-credit\"}", Encoding.UTF8, "application/json");
        using var creditReplay = await funding.PostAsync($"/api/v1/wallets/{source}/credits", replayBody);
        Assert.Equal(HttpStatusCode.OK, creditReplay.StatusCode);
        Assert.True(creditReplay.Headers.Contains("Idempotency-Replayed"));
        Assert.Equal(await credit.Content.ReadAsStringAsync(), await creditReplay.Content.ReadAsStringAsync());
        async Task<HttpResponseMessage> SendTransfer(string amount, string key)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/transfers");
            request.Headers.Add("Idempotency-Key", key);
            request.Content = new StringContent($"{{\"sourceWalletId\":\"{source}\",\"destinationWalletId\":\"{destination}\",\"amount\":{amount}}}", Encoding.UTF8, "application/json");
            return await customerA.SendAsync(request);
        }
        using var transfer = await SendTransfer("10", "naira-transfer");
        using var replay = await SendTransfer("10.00", "naira-transfer");
        Assert.Equal(HttpStatusCode.OK, transfer.StatusCode);
        using var transferJson = JsonDocument.Parse(await transfer.Content.ReadAsStringAsync());
        Assert.Equal(1000, transferJson.RootElement.GetProperty("amountKobo").GetInt64());
        Assert.Equal(9990.50m, transferJson.RootElement.GetProperty("sourceBalanceInNaira").GetDecimal());
        Assert.False(transferJson.RootElement.TryGetProperty("sourceBalanceAfterKobo", out _));
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.True(replay.Headers.Contains("Idempotency-Replayed"));
        Assert.Equal(await transfer.Content.ReadAsStringAsync(), await replay.Content.ReadAsStringAsync());
        using var conflict = await SendTransfer("10.01", "naira-transfer");
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        using var fractional = await SendTransfer("10.01", "naira-fractional");
        Assert.Equal(HttpStatusCode.OK, fractional.StatusCode);
        using var fractionalJson = JsonDocument.Parse(await fractional.Content.ReadAsStringAsync());
        Assert.Equal(1001, fractionalJson.RootElement.GetProperty("amountKobo").GetInt64());
        Assert.Equal(9980.49m, fractionalJson.RootElement.GetProperty("sourceBalanceInNaira").GetDecimal());
        foreach (var amount in new[] { "0", "-1", "10.001", "\"10\"", "92233720368547758.08", "1e4" })
        {
            using var invalid = await SendTransfer(amount, "naira-invalid");
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        }
        using var legacyBody = new StringContent("{\"amountKobo\":10000,\"reference\":\"legacy\"}", Encoding.UTF8, "application/json");
        using var legacy = await funding.PostAsync($"/api/v1/wallets/{source}/credits", legacyBody);
        Assert.Equal(HttpStatusCode.BadRequest, legacy.StatusCode);
        Assert.Equal(998_049, await BalanceAsync(customerA, source));
        Assert.Equal(2_001, await BalanceAsync(customerB, destination));
        Assert.Equal(2, await database.ScalarAsync("SELECT COUNT(*) FROM dbo.Transfers"));
        Assert.Equal(5, await database.ScalarAsync("SELECT COUNT(*) FROM dbo.AuditEntries"));
    }
    [Fact]
    public async Task CreditReturnsCumulativeNairaBalanceAndReplaysItsOriginalBalance()
    {
        var database = await sql.CreateDatabaseAsync();
        using var factory = new LedgerFactory(database);
        var customerId = Guid.NewGuid();
        using var customer = Client(factory, customerId);
        using var funding = Client(factory, Guid.NewGuid(), "FundingSystem");
        var wallet = await WalletAsync(customer, customerId);
        await CreditAsync(funding, wallet, 100);
        using var second = await funding.PostAsJsonAsync($"/api/v1/wallets/{wallet}/credits",
            new CreditWalletRequest(new NairaAmount(225), "cumulative-credit"));
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        using var json = JsonDocument.Parse(await second.Content.ReadAsStringAsync());
        Assert.Equal(225, json.RootElement.GetProperty("amountKobo").GetInt64());
        Assert.Equal(3.25m, json.RootElement.GetProperty("totalbalanceInNaira").GetDecimal());
        Assert.False(json.RootElement.TryGetProperty("balanceAfterKobo", out _));
        await CreditAsync(funding, wallet, 1);
        using var replay = await funding.PostAsJsonAsync($"/api/v1/wallets/{wallet}/credits",
            new CreditWalletRequest(new NairaAmount(225), "cumulative-credit"));
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.True(replay.Headers.Contains("Idempotency-Replayed"));
        Assert.Equal(await second.Content.ReadAsStringAsync(), await replay.Content.ReadAsStringAsync());
        Assert.Equal(326, await BalanceAsync(customer, wallet));
    }

    private static async Task<string> CodeAsync(HttpResponseMessage response)
    {
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("code").GetString()!;
    }

    [Fact]
    public async Task WalletCreationIsZeroAndDuplicateRecoversId()
    {
        var database = await sql.CreateDatabaseAsync();
        using var factory = new LedgerFactory(database);
        var customer = Guid.NewGuid();
        using var client = Client(factory, customer);
        var wallet = await WalletAsync(client, customer);
        using var duplicate = await client.PostAsJsonAsync("/api/v1/wallets", new CreateWalletRequest(customer));
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Contains(wallet.ToString(), duplicate.Headers.Location!.ToString());
        Assert.Equal(0, await BalanceAsync(client, wallet));
        Assert.Equal(1, await database.ScalarAsync("SELECT COUNT(*) FROM dbo.Wallets"));
        Assert.Equal(0, await database.ScalarAsync("SELECT COUNT(*) FROM dbo.AuditEntries"));
    }

    [Fact]
    public async Task ConcurrentOverspendingAllowsExactlyTenAndConservesMoney()
    {
        var database = await sql.CreateDatabaseAsync();
        using var factory = new LedgerFactory(database);
        var a = Guid.NewGuid(); var b = Guid.NewGuid();
        using var customerA = Client(factory, a);
        using var customerB = Client(factory, b);
        using var funding = Client(factory, Guid.NewGuid(), "FundingSystem");
        var source = await WalletAsync(customerA, a);
        var destination = await WalletAsync(customerB, b);
        await CreditAsync(funding, source, 10_000_000);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tasks = Enumerable.Range(0, 100).Select(async i =>
        {
            await gate.Task;
            return await TransferAsync(customerA, source, destination, 1_000_000, $"load-{i}");
        }).ToArray();
        gate.SetResult();
        var responses = await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(120));
        try
        {
            Assert.Equal(10, responses.Count(r => r.StatusCode == HttpStatusCode.OK));
            Assert.Equal(90, responses.Count(r => r.StatusCode == HttpStatusCode.UnprocessableEntity));
            foreach (var rejected in responses.Where(r => r.StatusCode != HttpStatusCode.OK))
                Assert.Equal("insufficient-funds", await CodeAsync(rejected));
            Assert.Equal(0, await BalanceAsync(customerA, source));
            Assert.Equal(10_000_000, await BalanceAsync(customerB, destination));
            Assert.Equal(10, await database.ScalarAsync("SELECT COUNT(*) FROM dbo.Transfers"));
            Assert.Equal(20, await database.ScalarAsync("SELECT COUNT(*) FROM dbo.WalletTransactions WHERE Type IN (2,3)"));
            Assert.Equal(20, await database.ScalarAsync("SELECT COUNT(*) FROM dbo.AuditEntries WHERE Operation IN (2,3)"));
            Assert.Equal(100, await database.ScalarAsync("SELECT COUNT(*) FROM dbo.IdempotencyRecords WHERE Scope LIKE 'transfer:%'"));
            Assert.Equal(0, await database.ScalarAsync("SELECT COUNT(*) FROM dbo.Wallets WHERE BalanceKobo < 0"));
            Assert.Equal(0, await database.ScalarAsync("""
                SELECT COUNT(*) FROM dbo.Wallets w WHERE w.BalanceKobo <>
                (SELECT COALESCE(SUM(CASE WHEN t.Type = 2 THEN -t.AmountKobo ELSE t.AmountKobo END),0)
                 FROM dbo.WalletTransactions t WHERE t.WalletId = w.Id)
                """));
        }
        finally { foreach (var response in responses) response.Dispose(); }
    }

    [Fact]
    public async Task FiftyIdenticalRequestsAcrossTwoHostsDebitOnce()
    {
        var database = await sql.CreateDatabaseAsync();
        using var factory1 = new LedgerFactory(database);
        using var factory2 = new LedgerFactory(database);
        var a = Guid.NewGuid(); var b = Guid.NewGuid();
        using var client1 = Client(factory1, a);
        using var client2 = Client(factory2, a);
        using var recipient = Client(factory1, b);
        using var funding = Client(factory1, Guid.NewGuid(), "FundingSystem");
        var source = await WalletAsync(client1, a);
        var destination = await WalletAsync(recipient, b);
        await CreditAsync(funding, source, 2_000_000);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tasks = Enumerable.Range(0, 50).Select(async i =>
        {
            await gate.Task;
            return await TransferAsync(i % 2 == 0 ? client1 : client2, source, destination, 1_000_000, "same-key");
        }).ToArray();
        gate.SetResult();
        var responses = await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(120));
        try
        {
            Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
            var bodies = await Task.WhenAll(responses.Select(r => r.Content.ReadAsStringAsync()));
            Assert.Single(bodies.Distinct());
            Assert.Equal(49, responses.Count(r => r.Headers.Contains("Idempotency-Replayed")));
            Assert.Equal(1_000_000, await BalanceAsync(client1, source));
            Assert.Equal(1_000_000, await BalanceAsync(recipient, destination));
            Assert.Equal(1, await database.ScalarAsync("SELECT COUNT(*) FROM dbo.Transfers"));
        }
        finally { foreach (var response in responses) response.Dispose(); }
    }

    [Fact]
    public async Task ChangedPayloadConflictsAndBusinessRejectionRemainsBound()
    {
        var database = await sql.CreateDatabaseAsync();
        using var factory = new LedgerFactory(database);
        var a = Guid.NewGuid(); var b = Guid.NewGuid();
        using var client = Client(factory, a);
        using var recipient = Client(factory, b);
        using var funding = Client(factory, Guid.NewGuid(), "FundingSystem");
        var source = await WalletAsync(client, a);
        var destination = await WalletAsync(recipient, b);
        using var rejected = await TransferAsync(client, source, destination, 100, "attempt");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, rejected.StatusCode);
        await CreditAsync(funding, source, 1000);
        using var replay = await TransferAsync(client, source, destination, 100, "attempt");
        Assert.Equal(await rejected.Content.ReadAsStringAsync(), await replay.Content.ReadAsStringAsync());
        using var changed = await TransferAsync(client, source, destination, 101, "attempt");
        Assert.Equal(HttpStatusCode.Conflict, changed.StatusCode);
        Assert.Equal("idempotency-conflict", await CodeAsync(changed));
        using var newAttempt = await TransferAsync(client, source, destination, 100, "new-attempt");
        Assert.Equal(HttpStatusCode.OK, newAttempt.StatusCode);
        Assert.Equal(900, await BalanceAsync(client, source));
    }

    [Fact]
    public async Task ConcurrentDailyLimitPermitsOnlyOneNearLimitTransfer()
    {
        var database = await sql.CreateDatabaseAsync();
        using var factory = new LedgerFactory(database);
        var a = Guid.NewGuid(); var b = Guid.NewGuid();
        using var client = Client(factory, a);
        using var recipient = Client(factory, b);
        using var funding = Client(factory, Guid.NewGuid(), "FundingSystem");
        var source = await WalletAsync(client, a);
        var destination = await WalletAsync(recipient, b);
        await CreditAsync(funding, source, 60_000_000);
        using var initial = await TransferAsync(client, source, destination, 45_000_000, "initial");
        Assert.Equal(HttpStatusCode.OK, initial.StatusCode);
        var responses = await Task.WhenAll(
            TransferAsync(client, source, destination, 4_000_000, "near-1"),
            TransferAsync(client, source, destination, 4_000_000, "near-2"));
        try
        {
            Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
            Assert.Equal("daily-limit-exceeded", await CodeAsync(responses.Single(r => r.StatusCode != HttpStatusCode.OK)));
            Assert.Equal(49_000_000, await BalanceAsync(recipient, destination));
            Assert.Equal(11_000_000, await BalanceAsync(client, source));
        }
        finally { foreach (var response in responses) response.Dispose(); }
    }

    [Fact]
    public async Task CreditsDeduplicateAcrossFundingActorsAndRejectOverflow()
    {
        var database = await sql.CreateDatabaseAsync();
        using var factory = new LedgerFactory(database);
        var a = Guid.NewGuid();
        using var customer = Client(factory, a);
        using var funding1 = Client(factory, Guid.NewGuid(), "FundingSystem");
        using var funding2 = Client(factory, Guid.NewGuid(), "FundingSystem");
        var wallet = await WalletAsync(customer, a);
        await CreditAsync(funding1, wallet, long.MaxValue, "reference");
        using var replay = await funding2.PostAsJsonAsync($"/api/v1/wallets/{wallet}/credits", new CreditWalletRequest(new NairaAmount(long.MaxValue), "reference"));
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.True(replay.Headers.Contains("Idempotency-Replayed"));
        using var overflow = await funding1.PostAsJsonAsync($"/api/v1/wallets/{wallet}/credits", new CreditWalletRequest(new NairaAmount(1), "overflow"));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, overflow.StatusCode);
        Assert.Equal("balance-capacity-exceeded", await CodeAsync(overflow));
        Assert.Equal(long.MaxValue, await BalanceAsync(customer, wallet));
        Assert.Equal(1, await database.ScalarAsync("SELECT COUNT(*) FROM dbo.AuditEntries"));
    }

    [Fact]
    public async Task JwtRolesOwnershipAndNairaContractsAreEnforced()
    {
        var database = await sql.CreateDatabaseAsync();
        using var factory = new LedgerFactory(database);
        var a = Guid.NewGuid();
        using var owner = Client(factory, a);
        using var stranger = Client(factory, Guid.NewGuid());
        using var anonymous = factory.CreateClient();
        var wallet = await WalletAsync(owner, a);
        using var unauthenticated = await anonymous.GetAsync($"/api/v1/wallets/{wallet}/balance");
        Assert.Equal(HttpStatusCode.Unauthorized, unauthenticated.StatusCode);
        Assert.Equal("unauthenticated", await CodeAsync(unauthenticated));
        using var forbidden = await stranger.GetAsync($"/api/v1/wallets/{wallet}/balance");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        using var selfFunding = await owner.PostAsJsonAsync($"/api/v1/wallets/{wallet}/credits", new CreditWalletRequest(new NairaAmount(100), "ref"));
        Assert.Equal(HttpStatusCode.Forbidden, selfFunding.StatusCode);
        using var ownAudit = await owner.GetAsync($"/api/v1/audit/wallets/{wallet}");
        Assert.Equal(HttpStatusCode.Forbidden, ownAudit.StatusCode);
        using var funding = Client(factory, Guid.NewGuid(), "FundingSystem");
        foreach (var value in new[] { "0", "-1", "0.001", "\"100\"", "9223372036854775808" })
        {
            using var content = new StringContent($"{{\"amount\":{value},\"reference\":\"bad\"}}", Encoding.UTF8, "application/json");
            using var response = await funding.PostAsync($"/api/v1/wallets/{wallet}/credits", content);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        Assert.Equal(0, await BalanceAsync(owner, wallet));
    }

    [Fact]
    public async Task AuditFailureRollsBackBalancesHistoryAndKey()
    {
        var database = await sql.CreateDatabaseAsync();
        using var factory = new LedgerFactory(database);
        var a = Guid.NewGuid(); var b = Guid.NewGuid();
        using var client = Client(factory, a);
        using var recipient = Client(factory, b);
        using var funding = Client(factory, Guid.NewGuid(), "FundingSystem");
        var source = await WalletAsync(client, a);
        var destination = await WalletAsync(recipient, b);
        await CreditAsync(funding, source, 1000);
        await database.ExecuteAsync("""
            CREATE TRIGGER dbo.TR_TestFailAudit ON dbo.AuditEntries AFTER INSERT
            AS BEGIN THROW 51000, 'Injected audit persistence failure.', 1; END;
            """);
        using var response = await TransferAsync(client, source, destination, 100, "rollback-test");
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(1000, await BalanceAsync(client, source));
        Assert.Equal(0, await BalanceAsync(recipient, destination));
        Assert.Equal(0, await database.ScalarAsync("SELECT COUNT(*) FROM dbo.Transfers"));
        Assert.Equal(0, await database.ScalarAsync("SELECT COUNT(*) FROM dbo.IdempotencyRecords WHERE Scope LIKE 'transfer:%'"));
        Assert.Equal(1, await database.ScalarAsync("SELECT COUNT(*) FROM dbo.AuditEntries"));
    }

    [Fact]
    public async Task RuntimePermissionsAndPrivilegedTriggersProtectAudit()
    {
        var database = await sql.CreateDatabaseAsync();
        using var factory = new LedgerFactory(database);
        var a = Guid.NewGuid();
        using var customer = Client(factory, a);
        using var funding = Client(factory, Guid.NewGuid(), "FundingSystem");
        var wallet = await WalletAsync(customer, a);
        await CreditAsync(funding, wallet, 1000);
        await Assert.ThrowsAsync<SqlException>(() => database.ExecuteAsync("UPDATE dbo.AuditEntries SET AmountKobo = AmountKobo", true));
        await Assert.ThrowsAsync<SqlException>(() => database.ExecuteAsync("DELETE FROM dbo.AuditEntries", true));
        await Assert.ThrowsAsync<SqlException>(() => database.ExecuteAsync("CREATE TABLE dbo.NotAllowed (Id int)", true));
        await Assert.ThrowsAsync<SqlException>(() => database.ExecuteAsync("DELETE FROM dbo.AuditEntries"));
        Assert.Equal(1, await database.ScalarAsync("SELECT COUNT(*) FROM dbo.AuditEntries"));
    }

    [Fact]
    public async Task StatementAuditSwaggerAndHealthAreReachable()
    {
        var database = await sql.CreateDatabaseAsync();
        using var factory = new LedgerFactory(database);
        var a = Guid.NewGuid();
        using var customer = Client(factory, a);
        using var funding = Client(factory, Guid.NewGuid(), "FundingSystem");
        using var auditor = Client(factory, Guid.NewGuid(), "Auditor");
        using var anonymous = factory.CreateClient();
        var wallet = await WalletAsync(customer, a);
        await CreditAsync(funding, wallet, 100);
        await CreditAsync(funding, wallet, 200);
        using var statement = await customer.GetAsync($"/api/v1/wallets/{wallet}/statement?page=1&pageSize=1");
        statement.EnsureSuccessStatusCode();
        using var body = JsonDocument.Parse(await statement.Content.ReadAsStringAsync());
        Assert.Equal(2, body.RootElement.GetProperty("totalCount").GetInt64());
        Assert.Equal(200, body.RootElement.GetProperty("items")[0].GetProperty("amountKobo").GetInt64());
        using var invalid = await customer.GetAsync($"/api/v1/wallets/{wallet}/statement?page=0");
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        using var audit = await auditor.GetAsync($"/api/v1/audit/wallets/{wallet}");
        Assert.Equal(HttpStatusCode.OK, audit.StatusCode);
        using var ready = await anonymous.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
        using var live = await anonymous.GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        using var swagger = await anonymous.GetAsync("/swagger/v1/swagger.json");
        Assert.Equal(HttpStatusCode.OK, swagger.StatusCode);
    }

    [Fact]
    public async Task RateLimiterReturnsProblemDetailsAndRetryAfter()
    {
        var database = await sql.CreateDatabaseAsync();
        using var factory = new LedgerFactory(database, 1);
        using var client = Client(factory, Guid.NewGuid());
        using var first = await TransferAsync(client, Guid.NewGuid(), Guid.NewGuid(), 1, "limit-1");
        Assert.Equal(HttpStatusCode.NotFound, first.StatusCode);
        using var second = await TransferAsync(client, Guid.NewGuid(), Guid.NewGuid(), 1, "limit-2");
        Assert.Equal(HttpStatusCode.TooManyRequests, second.StatusCode);
        Assert.Equal("rate-limit-exceeded", await CodeAsync(second));
        Assert.NotNull(second.Headers.RetryAfter);
    }

    [Fact]
    public async Task OpposingTransfersConserveBothBalances()
    {
        var database = await sql.CreateDatabaseAsync();
        using var factory = new LedgerFactory(database);
        var a = Guid.NewGuid(); var b = Guid.NewGuid();
        using var clientA = Client(factory, a);
        using var clientB = Client(factory, b);
        using var funding = Client(factory, Guid.NewGuid(), "FundingSystem");
        var walletA = await WalletAsync(clientA, a);
        var walletB = await WalletAsync(clientB, b);
        await CreditAsync(funding, walletA, 10_000_000);
        await CreditAsync(funding, walletB, 10_000_000);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tasks = Enumerable.Range(0, 50).Select(async i =>
        {
            await gate.Task;
            return await TransferAsync(i % 2 == 0 ? clientA : clientB,
                i % 2 == 0 ? walletA : walletB, i % 2 == 0 ? walletB : walletA, 100_000, $"opposing-{i}");
        }).ToArray();
        gate.SetResult();
        var responses = await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(120));
        try
        {
            Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
            Assert.Equal(10_000_000, await BalanceAsync(clientA, walletA));
            Assert.Equal(10_000_000, await BalanceAsync(clientB, walletB));
            Assert.Equal(50, await database.ScalarAsync("SELECT COUNT(*) FROM dbo.Transfers"));
        }
        finally { foreach (var response in responses) response.Dispose(); }
    }

    [Fact]
    public async Task SimultaneousConflictingPayloadsBindOnlyOne()
    {
        var database = await sql.CreateDatabaseAsync();
        using var factory = new LedgerFactory(database);
        var a = Guid.NewGuid(); var b = Guid.NewGuid();
        using var client = Client(factory, a);
        using var recipient = Client(factory, b);
        using var funding = Client(factory, Guid.NewGuid(), "FundingSystem");
        var source = await WalletAsync(client, a);
        var destination = await WalletAsync(recipient, b);
        await CreditAsync(funding, source, 1000);
        var responses = await Task.WhenAll(TransferAsync(client, source, destination, 100, "conflict"),
            TransferAsync(client, source, destination, 101, "conflict"));
        try
        {
            Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
            Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);
            Assert.Equal(1, await database.ScalarAsync("SELECT COUNT(*) FROM dbo.Transfers"));
            Assert.Equal(1000, await database.ScalarAsync("SELECT SUM(BalanceKobo) FROM dbo.Wallets"));
        }
        finally { foreach (var response in responses) response.Dispose(); }
    }

    [Fact]
    public async Task DatabaseUnavailableMakesReadinessFailWithoutFailingLiveness()
    {
        var database = await sql.CreateDatabaseAsync();
        var unavailable = new SqlConnectionStringBuilder(database.RuntimeConnection)
        { DataSource = "127.0.0.1,1", ConnectTimeout = 1 }.ConnectionString;
        using var factory = new LedgerFactory(database with { RuntimeConnection = unavailable });
        using var client = factory.CreateClient();
        using var ready = await client.GetAsync("/health/ready");
        using var live = await client.GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, ready.StatusCode);
        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
    }

    [Fact]
    public async Task DailyAllowanceResetsAtWatMidnightButOldRejectionDoesNot()
    {
        var database = await sql.CreateDatabaseAsync();
        var clock = new MutableClock(DateTimeOffset.Parse("2026-09-16T22:59:59Z"));
        using var factory = new LedgerFactory(database, clock: clock);
        var a = Guid.NewGuid(); var b = Guid.NewGuid();
        using var client = Client(factory, a);
        using var recipient = Client(factory, b);
        using var funding = Client(factory, Guid.NewGuid(), "FundingSystem");
        var source = await WalletAsync(client, a);
        var destination = await WalletAsync(recipient, b);
        await CreditAsync(funding, source, 100_000_000);
        using var dayOne = await TransferAsync(client, source, destination, 50_000_000, "day-one");
        Assert.Equal(HttpStatusCode.OK, dayOne.StatusCode);
        using var blocked = await TransferAsync(client, source, destination, 1, "blocked");
        Assert.Equal("daily-limit-exceeded", await CodeAsync(blocked));
        clock.Set(DateTimeOffset.Parse("2026-09-16T23:00:00Z"));
        using var dayTwo = await TransferAsync(client, source, destination, 1, "day-two");
        Assert.Equal(HttpStatusCode.OK, dayTwo.StatusCode);
        using var replay = await TransferAsync(client, source, destination, 1, "blocked");
        Assert.Equal(await blocked.Content.ReadAsStringAsync(), await replay.Content.ReadAsStringAsync());
        Assert.Equal(50_000_001, await BalanceAsync(recipient, destination));
    }
}
