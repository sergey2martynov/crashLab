using System.Data;
using System.Diagnostics;
using System.Security.Claims;
using CrashLab.Game.Balance;
using CrashLab.Game.Bets;
using CrashLab.GameEngine.Bets;
using CrashLab.GameEngine.Clients;
using CrashLab.GameEngine.Dtos;
using CrashLab.GameEngine.Grains;
using CrashLab.GameEngine.Metrics;
using CrashLab.GameEngine.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using OpenIddict.Abstractions;
using Polly.CircuitBreaker;

namespace CrashLab.GameEngine.Controllers;

[ApiController]
[Route("[controller]")]
[Authorize]
public class BetsController(
    ILogger<BetsController> logger,
    GameMetrics gameMetrics,
    IWalletClient walletClient,
    IOutboxRepository outboxRepository,
    IBetRepository betRepository,
    NpgsqlConnection connection,
    IGrainFactory grainFactory,
    ITableCatalog tableCatalog)
    : ControllerBase
{
    [HttpPost]
    public async Task<IResult> MapBetEndpoints(BetRequest request, CancellationToken ct)
    {
        try
        {
            var isOpen = await grainFactory.GetGrain<ITableManagerGrain>(0).IsOpen(request.TableId);
            if (!isOpen)
            {
                logger.LogWarning("Bet rejected: table {TableId} is closed", request.TableId);
                return Results.BadRequest("Table is closed");
            }
            
            var grain = grainFactory.GetGrain<IRoundGrain>(request.TableId);
            var state = await grain.GetState();
            var limits = tableCatalog.GetConfig(request.TableId);
            if (state.State == RoundState.WaitingForBets 
                && state.Id == request.RoundId && request.Amount >= limits.MinBet && request.Amount <= limits.MaxBet)
            {
                ThreadPool.GetAvailableThreads(out var workerThreads, out var ioThreads);
                logger.LogInformation("ThreadPool available: worker={Worker}, io={Io}", workerThreads, ioThreads);
                
                var time = Stopwatch.StartNew();
                var accountId = new Guid(User.FindFirstValue(OpenIddictConstants.Claims.Subject)!);
                await walletClient.DebitAsync(new DebitDto
                {
                    AccountId = accountId,
                    Amount = request.Amount,
                }, ct);
                logger.LogInformation("walletClient.DebitAsync: {Ms}ms", time.ElapsedMilliseconds);
                var bet = Bet.CreateBet(accountId, request.RoundId, request.Amount, request.TableId);
                logger.LogInformation("Created bet id: {Id}, amount: {Amount}", bet.Id, bet.Amount);

                if (connection.State != ConnectionState.Open)
                {
                    await connection.OpenAsync(ct);
                }

                await using var transaction = await connection.BeginTransactionAsync(ct);
                try
                {
                    await betRepository.AddAsync(bet, transaction, ct);
                    await outboxRepository.AddEventAsync("bet.placed", new
                    {
                        betId = bet.Id,
                        roundId = bet.RoundId,
                        accountId = bet.AccountId,
                        amount = bet.Amount
                    }, transaction);
                    await transaction.CommitAsync(ct);
                    await grainFactory.GetGrain<ITableManagerGrain>(0).RecordBet(request.TableId);
                }
                catch
                {
                    try
                    {
                        await walletClient.CompensateCreditAsync(new CompensationDto
                        {
                            AccountId = bet.AccountId,
                            Amount = bet.Amount,
                            CompensationId = bet.Id,
                        }, ct);
                    }
                    catch (Exception compEx)
                    {
                       logger.LogCritical(compEx, "Compensation credit FAILED — " +
                                                   "accountId: {AccountId}, amount: {Amount}, " +
                                                   "betId: {BetId}. Manual reconciliation needed.", 
                           bet.AccountId, bet.Amount, bet.Id);
                    }
                    
                    await transaction.RollbackAsync(ct);
                    throw;
                }
                
                time.Stop();
                gameMetrics.RecordBetDuration(time.Elapsed.TotalMilliseconds);
                return Results.Ok(bet);
            }

            logger.LogWarning(
                "Bet rejected by guard: state.State={State} (expected WaitingForBets), state.Id={StateId} vs request.RoundId={RequestRoundId}, amount={Amount} (limits {Min}-{Max})",
                state.State, state.Id, request.RoundId, request.Amount, limits.MinBet, limits.MaxBet);
        }
        catch (BrokenCircuitException)
        {
            logger.LogWarning("Wallet service circuit is open, rejecting request");
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Bet creating failed");
            return Results.BadRequest("Bet creating failed");
        }

        return Results.BadRequest("Something went wrong");
    }
    
    
    [HttpPost("{id}/cashout")]
    public async Task<IResult> MapBetEndpoints(string id, CancellationToken ct)
    {
        try
        {
            var bet = await betRepository.GetByIdAsync(new Guid(id), ct);
            
            if (bet is null)
            {
                return Results.BadRequest("Bet not created");
            }
            
            var accountId = new Guid(User.FindFirstValue(OpenIddictConstants.Claims.Subject)!);
            if (bet.AccountId != accountId)
                return Results.Forbid();
            
            var grain = grainFactory.GetGrain<IRoundGrain>(bet.TableId);
            var state = await grain.GetState();
            var multiplier = await grain.GetCurrentMultiplier(DateTimeOffset.Now);

            if (bet!.Status == BetStatus.Active
                && bet.RoundId == state.Id
                && state.State == RoundState.Running
                && multiplier < state.CrashPoint)  
            {
                var time = Stopwatch.StartNew();
                var cashOut = bet.Amount * Convert.ToDecimal(multiplier);
                await connection.CloseAsync();
                await walletClient.CashOutAsync(new CashOutDto
                {
                    AccountId = bet.AccountId,
                    Amount = cashOut,
                    CashOutId = bet.Id,
                }, ct);
                
                if (connection.State != ConnectionState.Open) await connection.OpenAsync(ct);
                
                await using var transaction = await connection.BeginTransactionAsync(ct);
                try
                {
                    
                    await betRepository.MarkCashedOutAsync(bet.Id, cashOut, transaction, ct);
                    await outboxRepository.AddEventAsync("bet.cashed_out", new
                    {
                        betId = bet.Id,
                        roundId = bet.RoundId,
                        accountId = bet.AccountId,
                        amount = cashOut
                    }, transaction);
                    await transaction.CommitAsync(ct);
                }
                catch
                {
                    try
                    {
                        await walletClient.CompensateDebitAsync(new CompensationDto
                        {
                            AccountId = bet.AccountId,
                            Amount = cashOut,
                            CompensationId = bet.Id,
                        }, ct);
                    }
                    catch (Exception compEx)
                    {
                        logger.LogCritical(compEx, "Compensation debit FAILED — " +
                                                    "accountId: {AccountId}, amount: {Amount}, " +
                                                    "betId: {BetId}. Manual reconciliation needed.", 
                            bet.AccountId, cashOut, bet.Id);
                    }
                    
                    await transaction.RollbackAsync(ct);
                    throw;
                }
                
                logger.LogInformation("Cashed out bet id: {Id}, cash out: {CashOut}", bet.Id, cashOut);
                time.Stop();
                gameMetrics.RecordBetDuration(time.Elapsed.TotalMilliseconds);
                return Results.Ok(cashOut);
            }

            logger.LogWarning(
                "Cashout rejected by guard: bet.Status={Status} (expected Active), bet.RoundId={BetRoundId} vs state.Id={StateId}, state.State={State} (expected Running), multiplier={Multiplier} vs CrashPoint={CrashPoint}",
                bet.Status, bet.RoundId, state.Id, state.State, multiplier, state.CrashPoint);
        }
        catch (BrokenCircuitException)
        {
            logger.LogWarning("Wallet service circuit is open, rejecting request");
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Cashout failed for bet {BetId}", id);
            return Results.BadRequest("Something went wrong.");
        }

        return Results.BadRequest("Something went wrong.");
    }
}

    