using System.Text.Json;
using System.Text.Json.Serialization;
using Confluent.Kafka;
using CrashLab.Game.Metrics;
using CrashLab.RealTimeGateWay;
using CrashLab.RealTimeGateWay.Hubs;
using CrashLab.RealTimeGateWay.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Validation;
using OpenIddict.Validation.AspNetCore;
using OpenTelemetry.Metrics;
using Serilog;
using StackExchange.Redis;
using IUserIdProvider = Microsoft.AspNetCore.SignalR.IUserIdProvider;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();
var redis = ConnectionMultiplexer.Connect(builder.Configuration["Redis:ConnectionString"]!);
builder.Services.AddSingleton<IConnectionMultiplexer>(redis);

builder.Services.AddOpenIddict()
    .AddValidation(options =>
    {
        var devSecret = builder.Configuration["DevelopmentSecretCert"];
        options.SetIssuer("http://identity.crashlab.local:8090/");
        options.AddEncryptionKey(new SymmetricSecurityKey(Convert.FromBase64String(devSecret)));
        options.UseSystemNetHttp();
        options.UseAspNetCore();

        
    });

builder.Services.AddAuthentication(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
builder.Services.AddAuthorization();

builder.Services.AddSingleton<GameMetrics>();
builder.Services.AddHostedService<HostedService>();
builder.Services.AddSingleton<IUserIdProvider, AccountIdUserIdProvider>();
builder.Services.AddHostedService<ConsumerService>();
var producerConfig = new ProducerConfig { BootstrapServers = builder.Configuration["Kafka:BootstrapServers"] };
builder.Services.AddSingleton<IProducer<string, string>>(_ =>
    new ProducerBuilder<string, string>(producerConfig).Build());

builder.Services.AddSignalR().AddJsonProtocol(options =>
{
    options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Host.UseSerilog((context, configuration) => configuration
    .WriteTo.Console()
    .WriteTo.File("logs/log-.txt", rollingInterval: RollingInterval.Day));
builder.Services.AddOpenTelemetry().WithMetrics(metrics => metrics
    .AddMeter(GameMetrics.MeterName)
    .AddPrometheusExporter());

var app = builder.Build();
app.MapHealthChecks("/health");
app.MapHub<GameHub>("/gamehub");
// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    //app.UseSwagger();
    //app.UseSwaggerUI();
}
app.MapPrometheusScrapingEndpoint();
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseHttpsRedirection();

app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/gamehub")
        && context.Request.Query.TryGetValue("access_token", out var token))
    {
        context.Request.Headers.Authorization = $"Bearer {token}";
    }
    await next();
});
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();