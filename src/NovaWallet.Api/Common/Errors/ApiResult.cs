using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;

namespace NovaWallet.Api.Common.Errors;

public static class ApiJson
{
    public static readonly JsonSerializerOptions Options = Create();
    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}

public sealed record ApiResult(int Status, object Body, bool Replayed = false, string? Location = null,
    string? StoredJson = null, string ContentType = "application/json")
{
    public string Json => StoredJson ?? JsonSerializer.Serialize(Body, ApiJson.Options);
    public static ApiResult Ok(object body) => new(200, body);
    public IActionResult ToActionResult(HttpContext context)
    {
        if (Replayed) context.Response.Headers["Idempotency-Replayed"] = "true";
        if (Location is not null) context.Response.Headers.Location = Location;
        return new ContentResult { StatusCode = Status, Content = Json, ContentType = ContentType };
    }
}

public static class ApiErrors
{
    public static string TraceId(HttpContext context) =>
        Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;

    public static ApiResult Create(HttpContext context, int status, string code, string detail)
    {
        var problem = new ProblemDetails
        {
            Type = $"urn:novawallet:problem:{code}",
            Title = code.Replace('-', ' '),
            Status = status,
            Detail = detail,
            Instance = context.Request.Path
        };
        problem.Extensions["code"] = code;
        problem.Extensions["traceId"] = TraceId(context);
        return new ApiResult(status, problem, ContentType: "application/problem+json");
    }
    public static Task WriteAsync(HttpContext context, int status, string code, string detail)
    {
        var result = Create(context, status, code, detail);
        context.Response.StatusCode = status;
        context.Response.ContentType = result.ContentType;
        return context.Response.WriteAsync(result.Json, context.RequestAborted);
    }
}
