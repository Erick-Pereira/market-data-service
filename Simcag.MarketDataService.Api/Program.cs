using DotNetEnv;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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

static IConnectionMultiplexer CreateRedisMultiplexer(string connectionString)
{
    var options = ConfigurationOptions.Parse(connectionString);
    // Dev/safe default: não rebentar o host se Redis estiver offline; reconexão em background.
    options.AbortOnConnectFail = false;
    if (options.ConnectTimeout <= 0)
        options.ConnectTimeout = 5000;
    return ConnectionMultiplexer.Connect(options);
}

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Database (POSTGRES_CONNECTION wins when set; otherwise DB__* fields)
var connectionString = GetEnv("POSTGRES_CONNECTION", "POSTGRES__CONNECTION", "DATABASE_URL")
                       ?? $"Host={Environment.GetEnvironmentVariable("DB__HOST")};" +
                          $"Port={Environment.GetEnvironmentVariable("DB__PORT")};" +
                          $"Database={Environment.GetEnvironmentVariable("DB__NAME")};" +
                          $"Username={Environment.GetEnvironmentVariable("DB__USER")};" +
                          $"Password={Environment.GetEnvironmentVariable("DB__PASSWORD")}";

builder.Services.AddDbContext<MarketDataDbContext>(options =>
    options.UseNpgsql(connectionString));

// Redis Cache (AbortOnConnectFail=false: API sobe mesmo sem Redis; cache degrada até o servidor ficar disponível)
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
{
    var redisConnectionString = GetEnv("REDIS_CONNECTION", "REDIS__CONNECTION") ?? "localhost:6379";
    return CreateRedisMultiplexer(redisConnectionString);
});

builder.Services.AddSingleton<IMarketDataCacheService, MarketDataCacheService>();

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

    var mux = sp.GetRequiredService<IConnectionMultiplexer>();
    var logger = sp.GetRequiredService<ILogger<RedisMarketDataUpdatedEventPublisher>>();
    return new RedisMarketDataUpdatedEventPublisher(mux, logger);
});

// Health Checks (Redis via .env; sem appsettings)
var redisForHealth = GetEnv("REDIS_CONNECTION", "REDIS__CONNECTION", "CONNECTIONSTRINGS__REDIS") ?? "localhost:6379";
builder.Services.AddHealthChecks()
    .AddNpgSql(connectionString, name: "PostgreSQL")
    .AddRedis(redisForHealth, name: "Redis");

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

if (app.Environment.IsDevelopment())
{
    app.MapGet("/", () => Results.Redirect("/swagger"));
}

app.Run();
