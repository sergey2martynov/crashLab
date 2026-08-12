using System.Security.Claims;
using CrashLab.WalletService.Exceptions;
using CrashLab.WalletService.Models;
using CrashLab.WalletService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;

namespace CrashLab.WalletService.Controllers;

[ApiController]
[Authorize]
[Route("[controller]")]
public class BalanceController : ControllerBase
{
    private readonly ILogger<BalanceController> _logger;
    private readonly IWalletService _walletService;

    public BalanceController(ILogger<BalanceController> logger,
        IWalletService walletService)
    {
        _logger = logger;
        _walletService = walletService;
    }

    [HttpGet("{accountId:guid}")]
    public async Task<ActionResult<Wallet>> GetBalance(CancellationToken ct = default)
    {
        var accountId = new Guid(User.FindFirstValue(OpenIddictConstants.Claims.Subject)!);
        var wallet = await _walletService.GetBalanceAsync(accountId, ct);
        return Ok(wallet);
    }

    [HttpPost("credit")]
    public async Task<ActionResult<Wallet>> Credit([FromBody] CreditDto dto, CancellationToken ct = default)
    {
        var accountId = new Guid(User.FindFirstValue(OpenIddictConstants.Claims.Subject)!);
        var wallet = await _walletService.CreditAsync(accountId, dto.Amount, ct);
        return Ok(wallet);
    }
    
    [HttpPost("cashout")]
    public async Task<ActionResult<Wallet>> Cashout([FromBody] CashOutDto dto, CancellationToken ct = default)
    {
        var accountId = new Guid(User.FindFirstValue(OpenIddictConstants.Claims.Subject)!);
        var wallet = await _walletService.CashOutAsync(accountId, dto, ct);
        return Ok(wallet);
    }
    
    [HttpPost("debit")]
    public async Task<ActionResult<Wallet>> Debit([FromBody] DebitDto dto, CancellationToken ct = default)
    {
        var accountId = new Guid(User.FindFirstValue(OpenIddictConstants.Claims.Subject)!);
        _logger.LogInformation("BalanceController: entered Debit action account={AccountId} at {Time:O}", accountId, DateTimeOffset.UtcNow);
        var wallet = await _walletService.DebitAsync(accountId, dto, ct);
        return Ok(wallet);
    }
    
    [HttpPost("compensate-credit")]
    public async Task<ActionResult<Wallet>> CompensateCredit([FromBody] CompensationDto dto, CancellationToken ct = default)
    {
        var accountId = new Guid(User.FindFirstValue(OpenIddictConstants.Claims.Subject)!);
        var wallet = await _walletService.CompensateCreditAsync(accountId, dto, ct);
        return Ok(wallet);
    }

    [HttpPost("compensate-debit")]
    public async Task<ActionResult<Wallet>> CompensateDebit([FromBody] CompensationDto dto, CancellationToken ct = default)
    {
        var accountId = new Guid(User.FindFirstValue(OpenIddictConstants.Claims.Subject)!);
        var wallet = await _walletService.CompensateDebitAsync(accountId, dto, ct);
        return Ok(wallet);
    }
}
