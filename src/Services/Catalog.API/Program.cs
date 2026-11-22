using System.Text;
using Catalog.API.Data;
using Catalog.API.Endpoints;
using Catalog.API.Health;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Navnet på denne instans (catalogapi-1 eller catalogapi-2 i Docker). Det sendes med i hvert svar og i hver loglinje,
// så man kan se, at gatewayen fordeler kaldene på begge instanser.
var instanceName = builder.Configuration["InstanceName"] ?? Environment.MachineName;

// 0) Logning: konsol + Logstash (se "Serilog" i appsettings.*.json), samme opsætning som de andre services.
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Instance", instanceName));

// 1) Egen database i SQL Server, som begge instanser deler. Kun Catalog.API kender den.
builder.Services.AddDbContext<CatalogDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("CatalogDb")));

// Den ene kopi fra monolitten (se CatalogDatabaseInitializer).
builder.Services.AddScoped<IMonolithCatalogSource, SqlMonolithCatalogSource>();
builder.Services.AddScoped<MonolithCatalogImporter>();

builder.Services.AddSingleton(new PictureUriComposer(builder.Configuration["CatalogBaseUrl"] ?? string.Empty));

// 2) Opret, ret og slet kræver et JWT fra PublicApi (POST /api/authenticate) med rollen Administrators.
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
    // Samme rollenavn som i monolitten (BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS).
    options.AddPolicy("admin", policy => policy.RequireRole("Administrators")));

// 3) Uventede fejl svarer med JSON (ProblemDetails) i stedet for en tom side, og de logges.
builder.Services.AddProblemDetails();

// 4) /health: kan katalogtabellen læses? Gatewayen bruger svaret til at vælge instans.
builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database");

// 5) OpenAPI-beskrivelse og Swagger UI på /swagger. Tryk "Authorize" og indsæt tokenet fra PublicApi.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Catalog.API", Version = "v1" });
    var bearer = new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "JWT fra PublicApi: POST /api/authenticate",
        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
    };
    c.AddSecurityDefinition("Bearer", bearer);
    c.AddSecurityRequirement(new OpenApiSecurityRequirement { [bearer] = Array.Empty<string>() });
});

var app = builder.Build();

app.Logger.LogInformation("Catalog.API instance {Instance} starting", instanceName);

// 6) Migrations og kopien fra monolitten (kun på den instans, der har Catalog:InitializeDatabase=true).
try
{
    await CatalogDatabaseInitializer.InitializeAsync(app.Services, app.Configuration, app.Logger);
}
catch (Exception ex)
{
    app.Logger.LogCritical(ex, "Could not prepare the catalog database; Catalog.API stops");
    throw;
}

app.UseExceptionHandler();

// Viser i svaret, hvilken instans der svarede (prøv: curl -i http://localhost:5106/api/catalog-brands).
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Catalog-Instance"] = instanceName;
    await next(context);
});

// Én linje pr. HTTP-kald i loggen (metode, sti, statuskode, varighed).
app.UseSerilogRequestLogging();

app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => $"Catalog.API kører ({instanceName})");
app.MapHealthChecks("/health");
app.MapCatalogEndpoints();

app.Run();

// Gør Program synlig for WebApplicationFactory i Catalog.API.UnitTests.
public partial class Program { }
