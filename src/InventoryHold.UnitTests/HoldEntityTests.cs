using InventoryHold.Contracts;
using InventoryHold.Domain.Entities;

namespace UnitTests;

public class HoldEntityTests
{
    [Fact]
    public void Hold_IsActive_ReturnsTrueWhenStatusActive()
    {
        var hold = new Hold { Status = HoldStatus.Active };
        Assert.True(hold.IsActive);
    }

    [Fact]
    public void Hold_IsActive_ReturnsFalseWhenReleased()
    {
        var hold = new Hold { Status = HoldStatus.Released };
        Assert.False(hold.IsActive);
    }

    [Fact]
    public void Hold_Release_SetsStatusAndTimestamp()
    {
        // Arrange
        var hold = new Hold
        {
            Status = HoldStatus.Active,
            CreatedAt = DateTime.UtcNow.AddMinutes(-5),
            ExpiresAt = DateTime.UtcNow.AddMinutes(10)
        };

        // Act
        hold.Release();

        // Assert
        Assert.Equal(HoldStatus.Released, hold.Status);
        Assert.NotNull(hold.ReleasedAt);
        Assert.True(hold.ReleasedAt <= DateTime.UtcNow);
    }

    [Fact]
    public void Hold_Release_ThrowsWhenNotActive()
    {
        var hold = new Hold { Status = HoldStatus.Expired };
        Assert.Throws<InvalidOperationException>(() => hold.Release());
    }

    [Fact]
    public void Hold_Expire_SetsStatusToExpired()
    {
        var hold = new Hold { Status = HoldStatus.Active };
        hold.Expire();
        Assert.Equal(HoldStatus.Expired, hold.Status);
    }

    [Fact]
    public void Hold_Expire_ThrowsWhenNotActive()
    {
        var hold = new Hold { Status = HoldStatus.Released };
        Assert.Throws<InvalidOperationException>(() => hold.Expire());
    }

    [Fact]
    public void Hold_IsExpired_TrueWhenPastExpiresAt()
    {
        var hold = new Hold { ExpiresAt = DateTime.UtcNow.AddMinutes(-1) };
        Assert.True(hold.IsExpired);
    }

    [Fact]
    public void Hold_IsExpired_FalseWhenNotYetExpired()
    {
        var hold = new Hold { ExpiresAt = DateTime.UtcNow.AddMinutes(10) };
        Assert.False(hold.IsExpired);
    }
}
