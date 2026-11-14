using PlayniteCmdPal.Core;
using Xunit;

namespace PlayniteCmdPal.Core.Tests;

public sealed class PlayniteLibraryNavigationStateTests
{
    [Fact]
    public void StartsInSearchWithEmptyQuery()
    {
        var navigation = new PlayniteLibraryNavigationState();

        Assert.Equal(
            new PlayniteLibraryNavigationSnapshot(PlayniteLibraryViewMode.Search, string.Empty),
            navigation.Capture());
    }

    [Fact]
    public void SwitchingViewsPreservesSharedSearchText()
    {
        var navigation = new PlayniteLibraryNavigationState();
        navigation.SetSearchText("  portal  ");

        Assert.True(navigation.SwitchView(PlayniteLibraryViewMode.RecentlyPlayed));
        Assert.Equal(
            new PlayniteLibraryNavigationSnapshot(PlayniteLibraryViewMode.RecentlyPlayed, "  portal  "),
            navigation.Capture());

        Assert.True(navigation.SwitchView(PlayniteLibraryViewMode.Favorites));
        Assert.Equal(
            new PlayniteLibraryNavigationSnapshot(PlayniteLibraryViewMode.Favorites, "  portal  "),
            navigation.Capture());
    }

    [Fact]
    public void SwitchingToCurrentViewIsANoOp()
    {
        var navigation = new PlayniteLibraryNavigationState();
        navigation.SetSearchText("Hades");

        Assert.False(navigation.SwitchView(PlayniteLibraryViewMode.Search));
        Assert.Equal("Hades", navigation.Capture().SearchText);
    }

    [Fact]
    public void RejectsUnknownViewMode()
    {
        var navigation = new PlayniteLibraryNavigationState();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            navigation.SwitchView((PlayniteLibraryViewMode)999));
    }
}
