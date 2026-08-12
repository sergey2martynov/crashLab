using Microsoft.AspNetCore.SignalR;
using OpenIddict.Abstractions;

namespace CrashLab.RealTimeGateWay;

public class AccountIdUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection)
    {
        return connection.User?.FindFirst(OpenIddictConstants.Claims.Subject)?.Value;
    }
}