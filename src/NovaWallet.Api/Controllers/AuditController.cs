using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NovaWallet.Api.Features.Audit.GetAuditTrail;

namespace NovaWallet.Api.Controllers;

[ApiController, Authorize(Policy = "Audit")]
[Route("api/v1/audit/wallets")]
public sealed class AuditController : ControllerBase
{
    [HttpGet("{walletId}")]
    public async Task<IActionResult> Get(Guid walletId, [FromServices] GetAuditTrailHandler handler, CancellationToken ct,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20) =>
        (await handler.HandleAsync(walletId, page, pageSize, HttpContext, ct)).ToActionResult(HttpContext);
}
