using PlayniteCmdPal.Core;
using Xunit;

namespace PlayniteCmdPal.Core.Tests;

public sealed class PlayniteLaunchUriTests
{
    private const string GameId = "11111111-1111-4111-8111-111111111111";

    [Fact]
    public void CreatesDocumentedLaunchUriFromCanonicalGameId()
    {
        var created = PlayniteLaunchUri.TryCreate(GameId, out var uri);

        Assert.True(created);
        Assert.NotNull(uri);
        Assert.Equal($"playnite://playnite/start/{GameId}", uri!.AbsoluteUri);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("11111111111141118111111111111111")]
    [InlineData("11111111-1111-4111-8111-11111111111A")]
    [InlineData(@"C:\Games\game.exe")]
    public void RejectsInvalidOrNonCanonicalGameIds(string? gameId)
    {
        Assert.False(PlayniteLaunchUri.TryCreate(gameId, out var uri));
        Assert.Null(uri);
    }
}
