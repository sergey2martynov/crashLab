using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));


builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("bets-per-user", httpContext =>
    {
        var accountId = httpContext.Request.Headers["X-Account-Id"].ToString();
        var key = string.IsNullOrEmpty(accountId)
            ? httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"
            : accountId;
        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromSeconds(5),
            QueueLimit = 0
        });
    });
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
    {
        var ip = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault()
                 ?? httpContext.Connection.RemoteIpAddress?.ToString()
                 ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 20,
            Window = TimeSpan.FromSeconds(10),
            QueueLimit = 0
        });
    });
});

var app = builder.Build();
app.UseRateLimiter();
app.MapReverseProxy();
app.Run();