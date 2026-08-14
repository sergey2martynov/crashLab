using CrashLab.WalletService.Exceptions;
using DbUp;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using OpenIddict.Validation.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
ThreadPool.SetMinThreads(1200, 1200);
// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();

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

builder.Host.UseSerilog((context, configuration) => configuration
    .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.AspNetCore.Hosting.Diagnostics", Serilog.Events.LogEventLevel.Information)
    .WriteTo.Console()
    .WriteTo.File("logs/log-.txt", rollingInterval: RollingInterval.Day));

var connectionString = builder.Configuration.GetConnectionString("Default")!;
builder.Services.AddSingleton(_ => new NpgsqlDataSourceBuilder(connectionString).Build());

builder.Services.AddScoped(sp =>
{
    var dataSource = sp.GetRequiredService<NpgsqlDataSource>();
    return dataSource.CreateConnection();
});
Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;

builder.Services.AddScoped<CrashLab.WalletService.Repositories.IWalletRepository, CrashLab.WalletService.Repositories.WalletRepository>();
builder.Services.AddScoped<CrashLab.WalletService.Services.IWalletService, CrashLab.WalletService.Services.WalletService>();

builder.Services.AddExceptionHandler<NotFoundExceptionHandler>();
builder.Services.AddExceptionHandler<InsufficientFundsExceptionHandler>();
builder.Services.AddProblemDetails();
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

app.MapHealthChecks("/health");

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseExceptionHandler();
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();