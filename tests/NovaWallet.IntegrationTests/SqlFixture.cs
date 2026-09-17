using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.TestHost;
using NovaWallet.Api.Infrastructure.Persistence;
using Testcontainers.MsSql;
using Xunit;

namespace NovaWallet.IntegrationTests;

[CollectionDefinition("SQL")]
public sealed class SqlCollection : ICollectionFixture<SqlFixture>;

public sealed class SqlFixture : IAsyncLifetime
{
    public const string RuntimePassword = "NovaWallet.Tests.Runtime!2026";
    private readonly MsSqlContainer container = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .WithPassword("NovaWallet.Tests.SA!2026")
        .WithEnvironment("MSSQL_MEMORY_LIMIT_MB", "1024")
        .Build();
    public Task InitializeAsync() => container.StartAsync();
    public Task DisposeAsync() => container.DisposeAsync().AsTask();

    public async Task<TestDatabase> CreateDatabaseAsync()
    {
        var privileged = new SqlConnectionStringBuilder(container.GetConnectionString())
        { InitialCatalog = "NovaWalletTest_" + Guid.NewGuid().ToString("N") }.ConnectionString;
        await DatabaseBootstrap.InitializeAsync(privileged, RuntimePassword);
        return new TestDatabase(privileged, DatabaseBootstrap.RuntimeConnection(privileged, RuntimePassword));
    }
}

public sealed record TestDatabase(string PrivilegedConnection, string RuntimeConnection)
{
    public async Task<long> ScalarAsync(string sql, bool runtime = false)
    {
        await using var connection = new SqlConnection(runtime ? RuntimeConnection : PrivilegedConnection);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }
    public async Task ExecuteAsync(string sql, bool runtime = false)
    {
        await using var connection = new SqlConnection(runtime ? RuntimeConnection : PrivilegedConnection);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }
}

public sealed class LedgerFactory(TestDatabase database, int permitLimit = 1000, TimeProvider? clock = null) : WebApplicationFactory<Program>
{
    public static readonly Dictionary<string, string?> TokenSettings = new()
    {
        ["Jwt:SigningKey"] = "NovaWallet.Tests.Signing.Key.Only.2026!",
        ["Jwt:Issuer"] = "novawallet-demo",
        ["Jwt:Audience"] = "novawallet-api"
    };
    public static IConfiguration TokenConfiguration => new ConfigurationBuilder().AddInMemoryCollection(TokenSettings).Build();
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        if (clock is not null)
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton(clock);
            });
    }
    protected override IHost CreateHost(IHostBuilder builder)
    {
        var settings = new Dictionary<string, string?>(TokenSettings)
        {
            ["ConnectionStrings:Ledger"] = database.RuntimeConnection,
            ["RateLimit:PermitLimit"] = permitLimit.ToString()
        };
        builder.ConfigureHostConfiguration(c => c.AddInMemoryCollection(settings));
        return base.CreateHost(builder);
    }
}

public sealed class MutableClock(DateTimeOffset now) : TimeProvider
{
    private long ticks = now.UtcTicks;
    public override DateTimeOffset GetUtcNow() => new(Interlocked.Read(ref ticks), TimeSpan.Zero);
    public void Set(DateTimeOffset value) => Interlocked.Exchange(ref ticks, value.UtcTicks);
}
