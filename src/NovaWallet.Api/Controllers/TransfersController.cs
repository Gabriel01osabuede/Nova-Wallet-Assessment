using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using NovaWallet.Api.Features.Transfers.TransferFunds;

namespace NovaWallet.Api.Controllers;

[ApiController, Authorize(Policy = "Customer")]
[Route("api/v1/transfers")]
public sealed class TransfersController : ControllerBase
{
    [HttpPost, EnableRateLimiting("Transfers")]
    public async Task<IActionResult> Transfer(TransferFundsRequest request, [FromServices] TransferFundsHandler handler, CancellationToken ct)
    {
        var keys = Request.Headers["Idempotency-Key"];
        var key = keys.Count == 1 ? keys[0] : null;
        return (await handler.HandleAsync(request, key, HttpContext, ct)).ToActionResult(HttpContext);
    }
}
