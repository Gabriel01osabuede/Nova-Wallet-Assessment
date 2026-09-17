using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using NovaWallet.Api.Common.Money;
using NovaWallet.Api.Common.Errors;
using NovaWallet.Api.Features.Audit.GetAuditTrail;
using NovaWallet.Api.Features.Statements.GetStatement;
using NovaWallet.Api.Features.Transfers.TransferFunds;
using NovaWallet.Api.Features.Wallets.CreateWallet;
using NovaWallet.Api.Features.Wallets.CreditWallet;
using NovaWallet.Api.Features.Wallets.GetBalance;
using NovaWallet.Api.Infrastructure.Authentication;
using NovaWallet.Api.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole();
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);

if (args.Contains("--migrate", StringComparer.Ordinal))
{
    await DatabaseBootstrap.InitializeAsync(
        builder.Configuration.GetConnectionString("Bootstrap") ?? throw new InvalidOperationException("Bootstrap connection is required."),
        builder.Configuration["Database:RuntimePassword"] ?? throw new InvalidOperationException("Runtime password is required."));
    return;
}

var signingKey = builder.Configuration["Jwt:SigningKey"] ?? throw new InvalidOperationException("JWT signing key is required.");
if (Encoding.UTF8.GetByteCount(signingKey) < 32 ||
    string.IsNullOrWhiteSpace(builder.Configuration["Jwt:Issuer"]) ||
    string.IsNullOrWhiteSpace(builder.Configuration["Jwt:Audience"]))
    throw new InvalidOperationException("JWT issuer, audience and a signing key of at least 256 bits are required.");
if (args.Length == 3 && args[0] == "--token")
{
    if (!builder.Environment.IsDevelopment()) throw new InvalidOperationException("Demo token minting is development-only.");
    Console.WriteLine(DemoTokens.Mint(builder.Configuration, Guid.Parse(args[1]), args[2], DateTimeOffset.UtcNow));
    return;
}

var dailyLimit = builder.Configuration.GetValue<long>("Ledger:DailyLimitKobo", 50_000_000);
var permits = builder.Configuration.GetValue<int>("RateLimit:PermitLimit", 200);
if (dailyLimit <= 0 || permits <= 0) throw new InvalidOperationException("Daily and request limits must be positive.");
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<CurrentCaller>();
builder.Services.AddDbContextFactory<LedgerDbContext>(options => options.UseSqlServer(
    builder.Configuration.GetConnectionString("Ledger") ?? throw new InvalidOperationException("Ledger connection is required."),
    sql => sql.CommandTimeout(30)));
builder.Services.AddScoped<FinancialTransaction>();
builder.Services.AddScoped<CreateWalletHandler>();
builder.Services.AddScoped<GetBalanceHandler>();
builder.Services.AddScoped<CreditWalletHandler>();
builder.Services.AddScoped<TransferFundsHandler>();
builder.Services.AddScoped<GetStatementHandler>();
builder.Services.AddScoped<GetAuditTrailHandler>();
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.NumberHandling = JsonNumberHandling.Strict;
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.Configure<ApiBehaviorOptions>(options =>
    options.InvalidModelStateResponseFactory = action =>
    {
        var errors = action.ModelState.Where(p => p.Value?.Errors.Count > 0)
            .ToDictionary(p => p.Key, p => p.Value!.Errors.Select(_ => "Invalid value.").ToArray());
        var problem = new ValidationProblemDetails(errors)
        {
            Type = "urn:novawallet:problem:invalid-request",
            Title = "Invalid request",
            Status = 400,
            Detail = "Correct the invalid request fields.",
            Instance = action.HttpContext.Request.Path
        };
        problem.Extensions["code"] = "invalid-request";
        problem.Extensions["traceId"] = ApiErrors.TraceId(action.HttpContext);
        return new BadRequestObjectResult(problem) { ContentTypes = { "application/problem+json" } };
    });
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.MapInboundClaims = false;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
        RoleClaimType = "role",
        NameClaimType = "sub",
        ClockSkew = TimeSpan.FromSeconds(30),
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256]
    };
    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = context =>
        {
            if (!Guid.TryParse(context.Principal?.FindFirst("sub")?.Value, out var id) || id == Guid.Empty)
                context.Fail("A valid subject identity is required.");
            return Task.CompletedTask;
        },
        OnChallenge = context =>
        {
            context.HandleResponse();
            context.Response.Headers.WWWAuthenticate = "Bearer";
            return ApiErrors.WriteAsync(context.HttpContext, 401, "unauthenticated", "A valid bearer token is required.");
        },
        OnForbidden = context => ApiErrors.WriteAsync(context.HttpContext, 403, "forbidden", "This operation is not permitted.")
    };
});
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Customer", p => p.RequireAuthenticatedUser().RequireRole("Customer")
        .RequireAssertion(c => Guid.TryParse(c.User.FindFirst("customer_id")?.Value, out var id) && id != Guid.Empty));
    options.AddPolicy("Funding", p => p.RequireAuthenticatedUser().RequireRole("FundingSystem", "Admin"));
    options.AddPolicy("Audit", p => p.RequireAuthenticatedUser().RequireRole("Auditor", "Admin"));
    options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
});
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("Transfers", context => RateLimitPartition.GetFixedWindowLimiter(
        context.User.FindFirst("customer_id")?.Value ?? "anonymous",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = permits, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    options.OnRejected = (context, ct) =>
    {
        var retry = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var duration) ? Math.Ceiling(duration.TotalSeconds) : 60;
        context.HttpContext.Response.Headers.RetryAfter = retry.ToString(CultureInfo.InvariantCulture);
        return new ValueTask(ApiErrors.WriteAsync(context.HttpContext, 429, "rate-limit-exceeded", "The transfer request allowance was exceeded."));
    };
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.MapType<NairaAmount>(() => new OpenApiSchema
    {
        Type = "number",
        Description = "Positive amount in NGN naira, with at most two decimal places. Use fixed-point notation; no strings or exponents. Converted exactly to integer kobo by the API.",
        Example = new Microsoft.OpenApi.Any.OpenApiInteger(10000),
        Minimum = 0.01m,
        Maximum = 92233720368547758.07m,
        MultipleOf = 0.01m
    });
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "NovaWallet Ledger API", Version = "v1" });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    { Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT" });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    { [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }] = [] });
    options.OperationFilter<NovaWallet.Api.Infrastructure.Authentication.IdempotencyHeaderFilter>();
});

var app = builder.Build();
app.Use(async (context, next) =>
{
    var traceId = ApiErrors.TraceId(context);
    context.Response.OnStarting(() => { context.Response.Headers["X-Trace-Id"] = traceId; return Task.CompletedTask; });
    var timer = Stopwatch.StartNew();
    try { await next(context); }
    catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { }
    catch (SqlException)
    {
        app.Logger.LogError("Database request failed {TraceId}", traceId);
        if (!context.Response.HasStarted)
            await ApiErrors.WriteAsync(context, 503, "datastore-unavailable", "Database unavailable. Retry financial operations using the same key.");
    }
    catch (Exception exception)
    {
        app.Logger.LogError("Unexpected request failure {ExceptionType} {SqlErrorNumber} {TraceId}",
            exception.GetType().Name, (exception as DbUpdateException)?.InnerException is SqlException sql ? sql.Number : null, traceId);
        if (!context.Response.HasStarted)
            await ApiErrors.WriteAsync(context, 500, "internal-error", "An unexpected error occurred.");
    }
    finally
    {
        app.Logger.LogInformation("Request completed {Method} {Path} {Status} {DurationMs} {TraceId}",
            context.Request.Method, context.Request.Path.Value, context.Response.StatusCode, timer.ElapsedMilliseconds, traceId);
    }
});
app.UseStatusCodePages(context => ApiErrors.WriteAsync(context.HttpContext, context.HttpContext.Response.StatusCode,
    "http-error", "The requested operation could not be completed."));
if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapControllers();
app.MapGet("/health/live", () => Results.Ok(new { status = "live" })).AllowAnonymous();
app.MapGet("/health/ready", async (IDbContextFactory<LedgerDbContext> factory, CancellationToken ct) =>
{
    try
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(3));
        await using var db = await factory.CreateDbContextAsync(timeout.Token);
        db.Database.SetCommandTimeout(3);
        await db.Database.ExecuteSqlRawAsync("SELECT TOP(1) Id FROM dbo.Wallets; SELECT TOP(1) MigrationId FROM dbo.__EFMigrationsHistory;", timeout.Token);
        return Results.Ok(new { status = "ready" });
    }
    catch { return Results.Json(new { status = "unready" }, statusCode: 503); }
}).AllowAnonymous();
app.Run();

public partial class Program;
