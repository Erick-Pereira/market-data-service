using System.Threading;
using DotNetEnv;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Simcag.MarketDataService.Application.Cache;
using Simcag.MarketDataService.Application.Catalog;
using Simcag.MarketDataService.Application.Classification;
using Simcag.MarketDataService.Application.Interfaces;
using Simcag.MarketDataService.Application.Ports;
using Simcag.MarketDataService.Application.Queries;
using Simcag.MarketDataService.Application.Services;
using Simcag.MarketDataService.Infrastructure.Messaging.Redis;
using Simcag.MarketDataService.Infrastructure.Persistence.DbContext;
using Simcag.MarketDataService.Infrastructure.Repositories;
using StackExchange.Redis;

DotNetEnv.Env.Load();
var builder = WebApplication.CreateBuilder(args);

static string? GetEnv(params string[] keys)
{
    foreach (var key in keys)
    {
        var v = Environment.GetEnvironmentVariable(key);
        if (!string.IsNullOrWhiteSpace(v))
            return v;
    }
    return null;
}

static string EnrichRedisConnectionString(string value)
{
    if (string.IsNullOrWhiteSpace(value))
        return "localhost:6379,abortConnect=false";
    if (value.Contains("abortConnect", StringComparison.OrdinalIgnoreCase))
        return value;
    var t = value.TrimEnd();
    return t + (t.Length == 0 || t[^1] == ',' ? "" : ",") + "abortConnect=false";
}

static IConnectionMultiplexer CreateRedisMultiplexer(string connectionString)
{
    var options = ConfigurationOptions.Parse(EnrichRedisConnectionString(connectionString));
    // Dev: não interromper o processo; reconexão em background quando o servidor subir.
    options.AbortOnConnectFail = false;
    if (options.ConnectTimeout <= 0)
        options.ConnectTimeout = 5000;
    return ConnectionMultiplexer.Connect(options);
}

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.OpenApiInfo { Title = "SIMC-AG Service", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.OpenApiSecurityScheme
    {
        Name = "Authorization",
        In = Microsoft.OpenApi.ParameterLocation.Header,
        Type = Microsoft.OpenApi.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Cole apenas o JWT (sem 'Bearer ')."
    });
    c.AddSecurityRequirement(document => new Microsoft.OpenApi.OpenApiSecurityRequirement
    {
        [new Microsoft.OpenApi.OpenApiSecuritySchemeReference("Bearer", document)] = []
    });
});

// PostgreSQL: ConnectionStrings__DefaultConnection tem prioridade; senão aliases legados (POSTGRES_CONNECTION/DB__*) com defaults.
var connectionString = GetEnv("ConnectionStrings__DefaultConnection", "CONNECTIONSTRINGS__DEFAULTCONNECTION", "POSTGRES_CONNECTION", "POSTGRES__CONNECTION", "DATABASE_URL");
if (string.IsNullOrWhiteSpace(connectionString))
{
    var host = GetEnv("DB__HOST", "DB_HOST") ?? "localhost";
    var port = GetEnv("DB__PORT", "DB_PORT") ?? "5432";
    var database = GetEnv("DB__NAME", "DB_NAME") ?? "simcag_market_data";
    var user = GetEnv("DB__USER", "DB_USER") ?? "postgres";
    var password = GetEnv("DB__PASSWORD", "DB_PASSWORD") ?? "postgres";
    connectionString = $"Host={host};Port={port};Database={database};Username={user};Password={password}";
}

builder.Services.AddDbContext<MarketDataDbContext>(options =>
    options.UseNpgsql(connectionString));

// Redis: se Connect falhar (nada a escutar em localhost, firewall, etc.), a API continua sem cache.
var redisConnectionString = GetEnv("REDIS__CONNECTION", "REDIS_CONNECTION", "CONNECTIONSTRINGS__REDIS")
    ?? "localhost:6379";
IConnectionMultiplexer? redisConnection = null;
try
{
    redisConnection = CreateRedisMultiplexer(redisConnectionString);
    // Com abortConnect=false o Connect() não lança, mas o servidor pode estar ausente: IsConnected fica false.
    // Espera curta e, se continuar desligado, tratar como "sem Redis" (evita timeouts de ~5s em cada cache GET).
    for (var i = 0; i < 20 && redisConnection is not null && !redisConnection.IsConnected; i++)
        Thread.Sleep(100);
    if (redisConnection is not null && !redisConnection.IsConnected)
    {
        Console.Error.WriteLine(
            "[market-data] Redis sem ligação ativa (ex.: nada a escutar em " + redisConnectionString + "). Cache desativado.");
        try
        {
            redisConnection.Dispose();
        }
        catch
        {
            // ignore
        }

        redisConnection = null;
    }
}
catch (Exception ex)
{
    Console.Error.WriteLine(
        "[market-data] Redis indisponível (" + ex.GetType().Name + ": " + ex.Message + "). Cache desativado; arranque o Redis ou corrija REDIS__CONNECTION.");
}

if (redisConnection is not null)
{
    builder.Services.AddSingleton(redisConnection);
    builder.Services.AddSingleton<IMarketDataCacheService, MarketDataCacheService>();
}
else
{
    builder.Services.AddSingleton<IMarketDataCacheService, NoOpMarketDataCacheService>();
}

builder.Services.AddSingleton<IMockMarketProductCatalog, MockMarketProductCatalog>();
builder.Services.AddSingleton<IRuleBasedExpenseCategoryClassifier, RuleBasedExpenseCategoryClassifier>();

// Repositories
builder.Services.AddScoped<IMarketPriceRepository, MarketPriceRepository>();
builder.Services.AddScoped<IMarketPriceHistoryRepository, MarketPriceHistoryRepository>();

// Services
builder.Services.AddScoped<IMarketDataService, MarketDataService>();
builder.Services.AddScoped<IMarketBenchmarkQuery, MarketBenchmarkQueryService>();

static bool IsTruthyEnv(string? v) =>
    !string.IsNullOrWhiteSpace(v) &&
    (string.Equals(v, "1", StringComparison.OrdinalIgnoreCase) ||
     string.Equals(v, "true", StringComparison.OrdinalIgnoreCase) ||
     string.Equals(v, "yes", StringComparison.OrdinalIgnoreCase));

builder.Services.AddSingleton<IMarketDataUpdatedEventPublisher>(sp =>
{
    if (!IsTruthyEnv(GetEnv("PUBLISH_MARKET_DATA_EVENTS")))
        return new NoOpMarketDataUpdatedEventPublisher();

    var mux = sp.GetService<IConnectionMultiplexer>();
    if (mux is null)
        return new NoOpMarketDataUpdatedEventPublisher();

    var logger = sp.GetRequiredService<ILogger<RedisMarketDataUpdatedEventPublisher>>();
    return new RedisMarketDataUpdatedEventPublisher(mux, logger);
});

// Health checks: Redis só se o multiplexer tiver sido criado (evita falha ao resolver o health check)
var health = builder.Services.AddHealthChecks()
    .AddNpgSql(connectionString, name: "PostgreSQL");
if (redisConnection is not null)
    health.AddRedis(EnrichRedisConnectionString(redisConnectionString), name: "Redis");

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapGet("/", () => Results.Redirect("/swagger"));
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();