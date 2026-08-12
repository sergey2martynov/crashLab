using Microsoft.AspNetCore.Diagnostics;

namespace CrashLab.WalletService.Exceptions;

public class InsufficientFundsExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken ct)
    {
        if (exception is not InsufficientFundsException) return false;
        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        await httpContext.Response.WriteAsJsonAsync(new { error = exception.Message }, ct);
        return true;
    }
}