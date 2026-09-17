using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NovaWallet.Api.Features.Wallets.CreateWallet;
using NovaWallet.Api.Features.Wallets.GetBalance;
using NovaWallet.Api.Features.Wallets.CreditWallet;
using NovaWallet.Api.Features.Statements.GetStatement;

namespace NovaWallet.Api.Controllers;

[ApiController]
[Route("api/v1/wallets")]
public sealed class WalletsController : ControllerBase
{
    [HttpPost, Authorize(Policy = "Customer")]
    public async Task<IActionResult> Create(CreateWalletRequest request, [FromServices] CreateWalletHandler handler, CancellationToken ct) =>
        (await handler.HandleAsync(request, HttpContext, ct)).ToActionResult(HttpContext);
    
    [HttpGet("{walletId}/balance"), Authorize(Policy = "Customer")]
    public async Task<IActionResult> Balance(Guid walletId, [FromServices] GetBalanceHandler handler, CancellationToken ct) =>
        (await handler.HandleAsync(walletId, HttpContext, ct)).ToActionResult(HttpContext);
   
    [HttpPost("{walletId}/credits"), Authorize(Policy = "Funding")]
    public async Task<IActionResult> Credit(Guid walletId, CreditWalletRequest request, [FromServices] CreditWalletHandler handler, CancellationToken ct) =>
        (await handler.HandleAsync(walletId, request, HttpContext, ct)).ToActionResult(HttpContext);
    
    [HttpGet("{walletId}/statement"), Authorize(Policy = "Customer")]
    public async Task<IActionResult> Statement(Guid walletId, [FromServices] GetStatementHandler handler,
        CancellationToken ct, [FromQuery] int page = 1, [FromQuery] int pageSize = 20) =>
        (await handler.HandleAsync(walletId, page, pageSize, HttpContext, ct)).ToActionResult(HttpContext);
}
