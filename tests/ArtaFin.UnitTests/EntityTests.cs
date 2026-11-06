using ArtaFin.Domain.Common;
using Xunit;

namespace ArtaFin.UnitTests;

public sealed class EntityTests
{
    [Fact]
    public void Equals_ReturnsTrue_ForSameTypeAndId()
    {
        var id = Guid.NewGuid();
        var first = new TestEntity(id, "First");
        var second = new TestEntity(id, "Second");

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void Equals_ReturnsFalse_ForDifferentIds()
    {
        var first = new TestEntity(Guid.NewGuid(), "Same");
        var second = new TestEntity(Guid.NewGuid(), "Same");

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Equals_ReturnsFalse_ForDifferentEntityTypesWithSameId()
    {
        var id = Guid.NewGuid();
        Entity first = new TestEntity(id, "Same");
        Entity second = new OtherTestEntity(id);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void HashSet_UsesEntityIdentity()
    {
        var id = Guid.NewGuid();
        var entities = new HashSet<Entity>
        {
            new TestEntity(id, "First"),
            new TestEntity(id, "Second")
        };

        Assert.Single(entities);
    }

    [Fact]
    public void Constructor_RejectsEmptyId()
    {
        Assert.Throws<ArgumentException>(() => new TestEntity(Guid.Empty, "Invalid"));
    }

    private sealed class TestEntity(Guid id, string name) : Entity(id)
    {
        public string Name { get; } = name;
    }

    private sealed class OtherTestEntity(Guid id) : Entity(id);
}
