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

    public async Task UpsertManyAsync(List<InventoryItem> items)
    {
        var bulkOps = items.Select(item => new UpdateOneModel<InventoryItem>(
            Builders<InventoryItem>.Filter.Eq(i => i.ProductId, item.ProductId),
            Builders<InventoryItem>.Update
                .SetOnInsert(i => i.Id, item.Id)
                .Set(i => i.ProductId, item.ProductId)
                .Set(i => i.ProductName, item.ProductName)
                .Set(i => i.AvailableQuantity, item.AvailableQuantity)
                .Set(i => i.ReservedQuantity, item.ReservedQuantity)
                .Set(i => i.TotalQuantity, item.TotalQuantity)
                .Set(i => i.UpdatedAt, item.UpdatedAt)
        ) { IsUpsert = true }).ToList();

        await _context.Inventory.BulkWriteAsync(bulkOps, new BulkWriteOptions { IsOrdered = false });
    }

    public async Task<bool> IsEmptyAsync()
    {
        var count = await _context.Inventory.CountDocumentsAsync(_ => true);
        return count == 0;
    }
}
