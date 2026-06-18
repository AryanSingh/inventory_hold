using MongoDB.Driver;
using InventoryHold.Domain.Entities;
using InventoryHold.Contracts;

namespace InventoryHold.Infrastructure.Persistence;

public class MongoDbContext
{
    private readonly IMongoDatabase _database;

    public MongoDbContext(IMongoDatabase database)
    {
        _database = database;
        EnsureIndexes();
    }

    public IMongoCollection<InventoryItem> Inventory => _database.GetCollection<InventoryItem>("inventory");
    public IMongoCollection<Hold> Holds => _database.GetCollection<Hold>("holds");

    private void EnsureIndexes()
    {
        // Inventory: unique index on ProductId for fast lookups and atomic decrements
        Inventory.Indexes.CreateMany(new[]
        {
            new CreateIndexModel<InventoryItem>(
                Builders<InventoryItem>.IndexKeys.Ascending(i => i.ProductId),
                new CreateIndexOptions { Unique = true, Name = "ix_inventory_productId" })
        });

        // Holds: unique index on HoldId + compound index for expiration polling
        Holds.Indexes.CreateMany(new[]
        {
            new CreateIndexModel<Hold>(
                Builders<Hold>.IndexKeys.Ascending(h => h.HoldId),
                new CreateIndexOptions { Unique = true, Name = "ix_holds_holdId" }),
            new CreateIndexModel<Hold>(
                Builders<Hold>.IndexKeys.Ascending(h => h.Status).Ascending(h => h.ExpiresAt),
                new CreateIndexOptions { Name = "ix_holds_status_expiresAt" })
        });
    }
}
