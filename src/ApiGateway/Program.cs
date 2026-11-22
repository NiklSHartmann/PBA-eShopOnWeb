using System.Text;
using ApiGateway.Logging;
using ApiGateway.RateLimiting;
using ApiGateway.Strangler;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// 0) Logning: konsol + Logstash (se "Serilog" i appsettings.*.json), samme opsætning som services.
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

// 1) Facaden: YARP læser routes og clusters fra "ReverseProxy" i appsettings.json.
//    CatalogStranglerFilter bestemmer, om katalog-ruterne går til Catalog.API eller til monolitten.
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddConfigFilter<CatalogStranglerFilter>();

// 2) Autentificering: samme JWT-nøgle som PublicApi, der udsteder tokens (POST /api/authenticate).
//    Routes med en AuthorizationPolicy afvises med 401/403 her, før de når en service.
var jwtSecretKey = builder.Configuration["Jwt:SecretKey"]
    ?? throw new InvalidOperationException("Jwt:SecretKey is missing from the configuration");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(jwtSecretKey)),
            ValidateIssuer = false,
            ValidateAudience = false
        };
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("authenticated", policy => policy.RequireAuthenticatedUser());
    // Samme rollenavn som i monolitten (BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS).
    options.AddPolicy("admin", policy => policy.RequireRole("Administrators"));
});

// 3) Rate limiting pr. IP-adresse (se RateLimiting/GatewayRateLimiting.cs og "RateLimits" i appsettings.json).
builder.Services.AddGatewayRateLimiting(builder.Configuration);

builder.Services.AddHealthChecks();

var app = builder.Build();

var useCatalogService = app.Configuration.GetValue<bool>(CatalogStranglerFilter.FlagName);
app.Logger.LogInformation("Gateway started; catalog routes go to cluster {CatalogCluster} ({FlagName}={UseCatalogService})",
    useCatalogService ? CatalogStranglerFilter.CatalogCluster : CatalogStranglerFilter.MonolithCluster,
    CatalogStranglerFilter.FlagName, useCatalogService);

// Én linje pr. kald i loggen (metode, route, statuskode, varighed) – også dem, der afvises med 401 eller 429.
app.UseRequestSummaryLogging();

// Rate limiting før autentificering, så også kald med forkerte tokens tæller med.
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// Gatewayens egen sundhed. /health sendes videre til Web ligesom alle andre stier.
app.MapHealthChecks("/gateway/health");
app.MapReverseProxy();

app.Run();
