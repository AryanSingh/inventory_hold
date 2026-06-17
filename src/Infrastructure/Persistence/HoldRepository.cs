using Contracts;
using Domain.Entities;
using Domain.Interfaces;
using MongoDB.Driver;

namespace Infrastructure.Persistence;

public class HoldRepository : IHoldRepository
{
    private readonly MongoDbContext _context;

    public HoldRepository(MongoDbContext context)
    {
        _context = context;
    }

    public async Task<Hold?> GetByHoldIdAsync(string holdId)
    {
        return await _context.Holds.Find(h => h.HoldId == holdId).FirstOrDefaultAsync();
    }

    public async Task<Hold> CreateAsync(Hold hold)
    {
        await _context.Holds.InsertOneAsync(hold);
        return hold;
    }

    public async Task<bool> ReleaseAsync(string holdId)
    {
        var filter = Builders<Hold>.Filter.And(
            Builders<Hold>.Filter.Eq(h => h.HoldId, holdId),
            Builders<Hold>.Filter.Eq(h => h.Status, HoldStatus.Active)
        );

        var update = Builders<Hold>.Update
            .Set(h => h.Status, HoldStatus.Released)
            .Set(h => h.ReleasedAt, DateTime.UtcNow);

        var result = await _context.Holds.UpdateOneAsync(filter, update);
        return result.ModifiedCount > 0;
    }

    public async Task<bool> MarkExpiredAsync(string holdId)
    {
        var filter = Builders<Hold>.Filter.Eq(h => h.HoldId, holdId);
        var update = Builders<Hold>.Update.Set(h => h.Status, HoldStatus.Expired);

        var result = await _context.Holds.UpdateOneAsync(filter, update);
        return result.ModifiedCount > 0;
    }

    public async Task<bool> TryMarkExpiredAsync(string holdId, HoldStatus expectedStatus)
    {
        // Optimistic locking: Only mark as expired if status hasn't changed
        var filter = Builders<Hold>.Filter.And(
            Builders<Hold>.Filter.Eq(h => h.HoldId, holdId),
            Builders<Hold>.Filter.Eq(h => h.Status, expectedStatus)
        );

        var update = Builders<Hold>.Update.Set(h => h.Status, HoldStatus.Expired);

        var result = await _context.Holds.UpdateOneAsync(filter, update);
        return result.ModifiedCount > 0;
    }

    public async Task<List<Hold>> FindExpiredAsync(DateTime asOf)
    {
        var filter = Builders<Hold>.Filter.And(
            Builders<Hold>.Filter.Eq(h => h.Status, HoldStatus.Active),
            Builders<Hold>.Filter.Lte(h => h.ExpiresAt, asOf)
        );

        return await _context.Holds.Find(filter).ToListAsync();
    }

    public async Task<List<Hold>> GetActiveHoldsAsync()
    {
        var filter = Builders<Hold>.Filter.Eq(h => h.Status, HoldStatus.Active);
        return await _context.Holds.Find(filter).ToListAsync();
    }
}
