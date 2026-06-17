using Infrastructure.Background;
using Infrastructure.Cache;
using Infrastructure.Messaging;
using Infrastructure.Persistence;
using Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using RabbitMQ.Client;

namespace Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // MongoDB
        var mongoConnectionString = configuration.GetConnectionString("MongoDb") ?? "mongodb://localhost:27017";
        var mongoClient = new MongoClient(mongoConnectionString);
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

        // RabbitMQ
        var rabbitMqConnectionString = configuration.GetConnectionString("RabbitMq") ?? "amqp://guest:guest@localhost:5672";
        var factory = new ConnectionFactory { Uri = new Uri(rabbitMqConnectionString) };
        var connection = factory.CreateConnectionAsync().GetAwaiter().GetResult();
        services.AddSingleton<IConnection>(connection);
        services.AddSingleton<IEventPublisher, RabbitMqPublisher>();

        // Background services
        services.AddHostedService<HoldExpirationService>();

        return services;
    }
}
