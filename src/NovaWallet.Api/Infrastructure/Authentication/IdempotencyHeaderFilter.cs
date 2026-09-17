using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace NovaWallet.Api.Infrastructure.Authentication;

public sealed class IdempotencyHeaderFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (context.ApiDescription.RelativePath != "api/v1/transfers") return;
        operation.Parameters.Add(new OpenApiParameter
        {
            Name = "Idempotency-Key",
            In = ParameterLocation.Header,
            Required = true,
            Description = "Customer-scoped, case-sensitive key; reuse only for identical requests.",
            Schema = new OpenApiSchema { Type = "string", MinLength = 1, MaxLength = 128 }
        });
    }
}
