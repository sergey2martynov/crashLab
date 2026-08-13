using CrashLab.IdentityServer.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var connectionString = builder.Configuration.GetConnectionString("Default")!;

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseNpgsql(connectionString);
    options.UseOpenIddict(); // регистрирует OpenIddict-сущности в модели EF
});

builder.Services.AddIdentity<IdentityUser, IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddOpenIddict()
    .AddCore(options => options.UseEntityFrameworkCore().UseDbContext<AppDbContext>())
    .AddServer(options =>
    {
        options.SetAuthorizationEndpointUris("connect/authorize")
            .SetTokenEndpointUris("connect/token");

        options.AllowAuthorizationCodeFlow()
            .RequireProofKeyForCodeExchange(); // PKCE

        options.RegisterScopes(OpenIddictConstants.Scopes.Profile);
        
        var devSecret = builder.Configuration["DevelopmentSecretCert"];
        options.AddEncryptionKey(new SymmetricSecurityKey(Convert.FromBase64String(devSecret!)));

        options.SetIssuer(new Uri("https://localhost:7289/"));
            
        options.AddDevelopmentEncryptionCertificate()
            .AddDevelopmentSigningCertificate();

        options.UseAspNetCore()
            .EnableAuthorizationEndpointPassthrough()
            .EnableTokenEndpointPassthrough();
    });

builder.Services.AddCors(options =>
{
    options.AddPolicy("spa", policy =>
    {
        policy.WithOrigins("http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();

    if (await manager.FindByClientIdAsync("crashlab-spa") is null)
    {
        await manager.CreateAsync(new OpenIddictApplicationDescriptor
        {
            ClientId = "crashlab-spa",
            ClientType = OpenIddictConstants.ClientTypes.Public, // SPA + PKCE, no client secret
            RedirectUris = { new Uri("http://localhost:5173/callback") },
            PostLogoutRedirectUris = { new Uri("http://localhost:5173/") },
            Permissions =
            {
                OpenIddictConstants.Permissions.Endpoints.Authorization,
                OpenIddictConstants.Permissions.Endpoints.Token,
                OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,
                OpenIddictConstants.Permissions.ResponseTypes.Code,
                OpenIddictConstants.Permissions.Scopes.Profile,
            },
            Requirements = { OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange },
        });
    }
}

using (var scope = app.Services.CreateScope())
{
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
    if (await userManager.FindByNameAsync("testuser") is null)
    {
        var user = new IdentityUser { UserName = "testuser", Email = "test@crashlab.local" };
        await userManager.CreateAsync(user, "Test123!");
    }
}

using (var scope = app.Services.CreateScope())
{
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
    if (await userManager.FindByNameAsync("testuser2") is null)
    {
        var user = new IdentityUser { UserName = "testuser2", Email = "test2@crashlab.local" };
        await userManager.CreateAsync(user, "Test123!");
    }
}   

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("spa");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();