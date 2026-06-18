using Infrastructure.Background;
using Infrastructure.Cache;
using Infrastructure.Messaging;
using Infrastructure.Persistence;
using Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;

namespace Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // MongoDB
        var mongoConnectionString = configuration.GetConnectionString("MongoDb")
            ?? throw new InvalidOperationException("Connection string 'MongoDb' is not configured.");
        var mongoClientSettings = MongoClientSettings.FromConnectionString(mongoConnectionString);
        mongoClientSettings.MaxConnectionPoolSize = 100;
        mongoClientSettings.MinConnectionPoolSize = 10;
        var mongoClient = new MongoClient(mongoClientSettings);
        var mongoDatabase = mongoClient.GetDatabase(configuration["MongoDb:DatabaseName"] ?? "inventory_hold_db");

        services.AddSingleton<IMongoClient>(mongoClient);
        services.AddSingleton(mongoDatabase);
        services.AddSingleton<MongoDbContext>();
        services.AddScoped<IInventoryRepository, InventoryRepository>();
        services.AddScoped<IHoldRepository, HoldRepository>();

        // Redis
        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = configuration.GetConnectionString("Redis") ?? "localhost:6379";
        });
        services.AddScoped<ICacheService, RedisCacheService>();

        // RabbitMQ — lazy-initialized to avoid sync-over-async at startup
        var rabbitMqConnectionString = configuration.GetConnectionString("RabbitMq")
            ?? throw new InvalidOperationException("Connection string 'RabbitMq' is not configured.");
        services.AddSingleton<IEventPublisher>(sp =>
            new RabbitMqPublisher(rabbitMqConnectionString));

        // Background services
        services.AddHostedService<HoldExpirationService>();

        return services;
    }
}
