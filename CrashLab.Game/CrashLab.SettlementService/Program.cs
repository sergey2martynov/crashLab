using Confluent.Kafka;
using CrashLab.SettlementService.Metrics;
using CrashLab.SettlementService.Repositories;
using CrashLab.SettlementService.Services;
using DbUp;
using Npgsql;
using OpenTelemetry.Metrics;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<SettlementMetrics>();
builder.Services.AddScoped<IBetRepository, BetRepository>();
builder.Services.AddScoped<IOutboxRepository, OutboxRepository>();
builder.Services.AddHostedService<OutboxPublisherService>();
builder.Services.AddHostedService<SettlementConsumerService>();

var producerConfig = new ProducerConfig { BootstrapServers = "localhost:9092" };
builder.Services.AddSingleton<IProducer<string, string>>(_ =>
    new ProducerBuilder<string, string>(producerConfig).Build());

builder.Host.UseSerilog((context, configuration) => configuration
    .WriteTo.Console()
    .WriteTo.File("logs/log-.txt", rollingInterval: RollingInterval.Day));
builder.Services.AddOpenTelemetry().WithMetrics(metrics => metrics
    .AddMeter(SettlementMetrics.MeterName)
    .AddPrometheusExporter());

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

app.MapPrometheusScrapingEndpoint();
// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();