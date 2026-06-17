using MongoDB.Driver;
using Domain.Entities;

namespace Infrastructure.Persistence;

public class MongoDbContext
{
    private readonly IMongoDatabase _database;

    public MongoDbContext(IMongoDatabase database)
    {
        _database = database;
    }

    public IMongoCollection<InventoryItem> Inventory => _database.GetCollection<InventoryItem>("inventory");
    public IMongoCollection<Hold> Holds => _database.GetCollection<Hold>("holds");
}
