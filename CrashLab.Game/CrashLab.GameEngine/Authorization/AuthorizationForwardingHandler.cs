namespace CrashLab.GameEngine.Authorization;

public class AuthorizationForwardingHandler(IHttpContextAccessor httpContextAccessor) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var incomingAuthHeader = httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();

        if (!string.IsNullOrEmpty(incomingAuthHeader))
        {
            request.Headers.TryAddWithoutValidation("Authorization", incomingAuthHeader);
        }

        return base.SendAsync(request, cancellationToken);
    }
}