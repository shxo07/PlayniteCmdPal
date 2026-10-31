using PlayniteCmdPal.Core;
using Xunit;

namespace PlayniteCmdPal.Core.Tests;

/// <summary>Contract tests for the SDK-independent game model.</summary>
public sealed class GameEntryTests
{
    [Fact]
    public void ValidateReturnsTheSameInstanceWhenRequiredFieldsArePresent()
    {
        var entry = new GameEntry("id", "Halo");

        Assert.Same(entry, entry.Validate());
        Assert.Same(entry, entry.Validate());
    }

    [Fact]
    public void OptionalFieldsDefaultToUnknownRatherThanInventedValues()
    {
        var entry = new GameEntry("id", "Halo");

        Assert.Null(entry.Source);
        Assert.Null(entry.IsInstalled);
        Assert.Null(entry.IconReference);
        Assert.Null(entry.Platform);
    }

    [Theory]
    [InlineData(null, "Halo", "GameId")]
    [InlineData("", "Halo", "GameId")]
    [InlineData("   ", "Halo", "GameId")]
    [InlineData("\t", "Halo", "GameId")]
    [InlineData("id", null, "Title")]
    [InlineData("id", "", "Title")]
    [InlineData("id", " \r\n", "Title")]
    public void ValidateRejectsMissingRequiredFields(string? gameId, string? title, string expectedParameterName)
    {
        var entry = new GameEntry(gameId!, title!);

        var error = Assert.Throws<ArgumentException>(entry.Validate);

        Assert.Equal(expectedParameterName, error.ParamName);
    }

    [Fact]
    public void RecordsUseValueEqualityIncludingOptionalFields()
    {
        var baseline = new GameEntry("id", "Halo", "Steam", true, "steam://icon", "PC");
        var duplicate = new GameEntry("id", "Halo", "Steam", true, "steam://icon", "PC");

        Assert.Equal(baseline, duplicate);
        Assert.Equal(baseline.GetHashCode(), duplicate.GetHashCode());
        Assert.NotEqual(baseline, baseline with { GameId = "other-id" });
        Assert.NotEqual(baseline, baseline with { Title = "Halo 2" });
        Assert.NotEqual(baseline, baseline with { Source = "GOG" });
        Assert.NotEqual(baseline, baseline with { IsInstalled = false });
        Assert.NotEqual(baseline, baseline with { IsInstalled = null });
        Assert.NotEqual(baseline, baseline with { IconReference = null });
        Assert.NotEqual(baseline, baseline with { Platform = "Switch" });
    }

    [Fact]
    public void WithExpressionKeepsStableIdentityWhenOnlyTheTitleChanges()
    {
        var entry = new GameEntry("id", "Halo", "Steam", true, null, "PC");

        var renamed = entry with { Title = "Halo: Combat Evolved" };

        Assert.Equal("id", renamed.GameId);
        Assert.Equal("Halo: Combat Evolved", renamed.Title);
        Assert.Equal("Steam", renamed.Source);
        Assert.True(renamed.IsInstalled);
        Assert.Equal("PC", renamed.Platform);
    }

    [Fact]
    public void TitlesAreNotNormalizedByTheModel()
    {
        // Normalization belongs to the snapshot contract; the model preserves what the
        // catalog supplied so identity and display text stay independently testable.
        var entry = new GameEntry("id", "  Halo  ");

        Assert.Equal("  Halo  ", entry.Validate().Title);
    }
}
