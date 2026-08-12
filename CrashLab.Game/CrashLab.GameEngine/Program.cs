using System.Text.Json.Serialization;
using Confluent.Kafka;
using CrashLab.Game.Balance;
using CrashLab.Game.Bets;
using CrashLab.GameEngine;
using CrashLab.GameEngine.Authorization;
using CrashLab.GameEngine.Clients;
using CrashLab.GameEngine.Grains;
using CrashLab.GameEngine.Metrics;
using CrashLab.GameEngine.Repositories;
using CrashLab.GameEngine.Services;
using DbUp;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using OpenIddict.Validation.AspNetCore;
using OpenTelemetry.Metrics;
using Polly;
using Serilog;
using StackExchange.Redis;
using WalletClient = CrashLab.GameEngine.Clients.WalletClient;

var builder = WebApplication.CreateBuilder(args);
ThreadPool.SetMinThreads(500, 500);
// Add services to the container.

builder.Services.AddHttpContextAccessor();

builder.Services.AddOpenIddict()
    .AddValidation(options =>
    {
        var devSecret = builder.Configuration["DevelopmentSecretCert"];
        options.SetIssuer("https://localhost:7289/");
        options.AddEncryptionKey(new SymmetricSecurityKey(Convert.FromBase64String(devSecret)));
        options.UseSystemNetHttp();
        options.UseAspNetCore();
    });

builder.Services.AddAuthentication(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
builder.Services.AddAuthorization();

builder.Services.AddTransient<AuthorizationForwardingHandler>();

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Host.UseOrleans(siloBuilder => siloBuilder.UseLocalhostClustering());
var redis = ConnectionMultiplexer.Connect("localhost:6379");
builder.Services.AddSingleton<IConnectionMultiplexer>(redis);

var producerConfig = new ProducerConfig { BootstrapServers = "localhost:9092" };
builder.Services.AddSingleton<IProducer<string, string>>(_ =>
    new ProducerBuilder<string, string>(producerConfig).Build());

builder.Services.AddSingleton<ITableCatalog, TableCatalog>();
builder.Services.AddScoped<IOutboxRepository, OutboxRepository>();
builder.Services.AddScoped<IBetRepository, BetRepository>();
builder.Services.AddHostedService<OutboxPublisherService>();

builder.Services.AddSingleton<BalanceStore>();
builder.Services.AddSingleton<GameMetrics>();
builder.Services.AddHostedService<GameLoopHostedService>();

builder.Services.AddHttpClient<IWalletClient, WalletClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["WalletConfiguration:BaseUrl"]!);
}).AddHttpMessageHandler<AuthorizationForwardingHandler>()
    .AddResilienceHandler("wallet-circuit-breaker", conf =>
{
    conf.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
    {
        FailureRatio = 0.5,
        MinimumThroughput = 10,
        SamplingDuration = TimeSpan.FromSeconds(10),
        BreakDuration = TimeSpan.FromSeconds(5)
    });
});
builder.Services.AddSignalR().AddJsonProtocol(options =>
{
    options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Host.UseSerilog((context, configuration) => configuration
    .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.AspNetCore.Hosting.Diagnostics", Serilog.Events.LogEventLevel.Information)
    .WriteTo.Console()
    .WriteTo.File("logs/log-.txt", rollingInterval: RollingInterval.Day));

builder.Services.AddOpenTelemetry().WithMetrics(metrics => metrics
    .AddMeter(GameMetrics.MeterName)
    .AddPrometheusExporter());
builder.Services.AddCors();



var connectionString = builder.Configuration.GetConnectionString("Default")!;
builder.Services.AddSingleton(_ => new NpgsqlDataSourceBuilder(connectionString).Build());
builder.Services.AddScoped(sp =>
{
    var dataSource = sp.GetRequiredService<NpgsqlDataSource>();
    return dataSource.CreateConnection();
});

var upgrader = DeployChanges.To
    .PostgresqlDatabase(connectionString)
    .WithScriptsEmbeddedInAssembly(System.Reflection.Assembly.GetExecutingAssembly())
    .LogToConsole()
    .Build();

var result = upgrader.PerformUpgrade();

if (!result.Successful)
{
    throw new Exception("Database migration failed", result.Error);
}

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapPrometheusScrapingEndpoint();
app.UseCors(conf => conf
    .WithOrigins("https://localhost:7297", "http://localhost:5195")
    .AllowAnyMethod()
    .AllowAnyHeader()); 

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();