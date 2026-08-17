using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Validation.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

builder.Services.AddOpenIddict()
    .AddValidation(options =>
    {
        var devSecret = builder.Configuration["DevelopmentSecretCert"];
        options.SetIssuer("http://identity.crashlab.local:8090/");
        options.AddEncryptionKey(new SymmetricSecurityKey(Convert.FromBase64String(devSecret!)));
        options.UseSystemNetHttp();
        options.UseAspNetCore();
    });

builder.Services.AddAuthentication(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
builder.Services.AddAuthorization();

builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("bets-per-user", httpContext =>
    {
        var accountId = httpContext.User.FindFirstValue(OpenIddictConstants.Claims.Subject);
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
});

builder.Services.AddHealthChecks();

builder.Services.AddCors(options =>
{
    options.AddPolicy("spa", policy =>
    {
        policy.WithOrigins("http://app.crashlab.local:8090")
            .AllowAnyHeader()
            .AllowCredentials()
            .AllowAnyMethod();
    });
});

var app = builder.Build();
app.MapHealthChecks("/health");
app.UseCors("spa");

app.UseAuthentication();
app.UseAuthorization();

app.UseRateLimiter();

app.MapReverseProxy();

app.Run();