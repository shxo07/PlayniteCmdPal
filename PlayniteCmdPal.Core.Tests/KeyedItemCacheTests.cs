using PlayniteCmdPal.Core;
using Xunit;

namespace PlayniteCmdPal.Core.Tests;

public sealed class KeyedItemCacheTests
{
    [Fact]
    public void GetOrAddReturnsSameInstanceForAnExistingKey()
    {
        var cache = new KeyedItemCache<object>();
        var original = new object();

        Assert.Same(original, cache.GetOrAdd("game-1", () => original));
        Assert.Same(original, cache.GetOrAdd("game-1", () => new object()));
        Assert.Equal(1, cache.Count);
    }

    [Fact]
    public void RetainKeysRemovesOnlyItemsMissingFromAuthoritativeSnapshot()
    {
        var cache = new KeyedItemCache<object>();
        var retained = new object();
        var removed = new object();
        cache.GetOrAdd("retained", () => retained);
        cache.GetOrAdd("removed", () => removed);

        cache.RetainKeys(["retained"]);

        Assert.Equal(1, cache.Count);
        Assert.Same(retained, cache.GetOrAdd("retained", () => new object()));
        var recreated = cache.GetOrAdd("removed", () => new object());
        Assert.NotSame(removed, recreated);
    }

    [Fact]
    public void RetainEmptySnapshotClearsAllCachedItems()
    {
        var cache = new KeyedItemCache<object>();
        cache.GetOrAdd("game-1", () => new object());
        cache.GetOrAdd("game-2", () => new object());

        cache.RetainKeys([]);

        Assert.Equal(0, cache.Count);
    }

    [Fact]
    public void RetainKeysDoesNotDiscardRowsStillPresentInCatalog()
    {
        var cache = new KeyedItemCache<object>();
        var first = new object();
        var second = new object();
        cache.GetOrAdd("game-1", () => first);
        cache.GetOrAdd("game-2", () => second);

        cache.RetainKeys(["game-1", "game-2", "new-game"]);

        Assert.Same(first, cache.GetOrAdd("game-1", () => new object()));
        Assert.Same(second, cache.GetOrAdd("game-2", () => new object()));
        Assert.Equal(2, cache.Count);
    }
}
