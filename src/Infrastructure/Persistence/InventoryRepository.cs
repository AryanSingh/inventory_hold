using Domain.Entities;
using Domain.Interfaces;
using MongoDB.Driver;

namespace Infrastructure.Persistence;

public class InventoryRepository : IInventoryRepository
{
    private readonly MongoDbContext _context;

    public InventoryRepository(MongoDbContext context)
    {
        _context = context;
    }

    public async Task<List<InventoryItem>> GetAllAsync()
    {
        return await _context.Inventory.Find(_ => true).ToListAsync();
    }

    public async Task<InventoryItem?> GetByProductIdAsync(string productId)
    {
        return await _context.Inventory.Find(i => i.ProductId == productId).FirstOrDefaultAsync();
    }

    public async Task<bool> DecrementAvailabilityAsync(string productId, int quantity)
    {
        var filter = Builders<InventoryItem>.Filter.And(
            Builders<InventoryItem>.Filter.Eq(i => i.ProductId, productId),
            Builders<InventoryItem>.Filter.Gte(i => i.AvailableQuantity, quantity)
        );

        var update = Builders<InventoryItem>.Update
            .Inc(i => i.AvailableQuantity, -quantity)
            .Inc(i => i.ReservedQuantity, quantity)
            .Set(i => i.UpdatedAt, DateTime.UtcNow);

        var result = await _context.Inventory.FindOneAndUpdateAsync(filter, update,
            new FindOneAndUpdateOptions<InventoryItem> { ReturnDocument = ReturnDocument.After });

        return result != null;
    }

    public async Task IncrementAvailabilityAsync(string productId, int quantity)
    {
        var filter = Builders<InventoryItem>.Filter.Eq(i => i.ProductId, productId);
        var update = Builders<InventoryItem>.Update
            .Inc(i => i.AvailableQuantity, quantity)
            .Inc(i => i.ReservedQuantity, -quantity)
            .Set(i => i.UpdatedAt, DateTime.UtcNow);

        await _context.Inventory.UpdateOneAsync(filter, update);
    }

    public async Task SeedAsync(List<InventoryItem> items)
    {
        await _context.Inventory.InsertManyAsync(items);
    }

    public async Task<bool> IsEmptyAsync()
    {
        var count = await _context.Inventory.CountDocumentsAsync(_ => true);
        return count == 0;
    }
}
