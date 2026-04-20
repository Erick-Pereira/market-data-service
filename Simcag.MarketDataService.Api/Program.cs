using Microsoft.EntityFrameworkCore;
using Simcag.MarketDataService.Application.Interfaces;
using Simcag.MarketDataService.Application.Services;
using Simcag.MarketDataService.Infrastructure.Persistence.DbContext;
using Simcag.MarketDataService.Infrastructure.Repositories;
using Simcag.Shared.Contracts;
using StackExchange.Redis;

DotNetEnv.Env.Load();

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Database
var connectionString = $"Host={Environment.GetEnvironmentVariable("DB__HOST")};" +
                      $"Port={Environment.GetEnvironmentVariable("DB__PORT")};" +
                      $"Database={Environment.GetEnvironmentVariable("DB__NAME")};" +
                      $"Username={Environment.GetEnvironmentVariable("DB__USER")};" +
                      $"Password={Environment.GetEnvironmentVariable("DB__PASSWORD")}";

builder.Services.AddDbContext<MarketDataDbContext>(options =>
    options.UseNpgsql(connectionString));

// Redis Cache
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var redisConnectionString = Environment.GetEnvironmentVariable("REDIS_CONNECTION") ?? "localhost:6379";
    return ConnectionMultiplexer.Connect(redisConnectionString);
});

builder.Services.AddSingleton<IMarketDataCacheService, MarketDataCacheService>();

// Repositories
builder.Services.AddScoped<IMarketPriceRepository, MarketPriceRepository>();
builder.Services.AddScoped<IMarketPriceHistoryRepository, MarketPriceHistoryRepository>();

// Services
builder.Services.AddScoped<IMarketDataService, MarketDataService>();

// Health Checks
builder.Services.AddHealthChecks()
    .AddNpgSql(connectionString, name: "PostgreSQL")
    .AddRedis(builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379", name: "Redis");

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

app.Run();
