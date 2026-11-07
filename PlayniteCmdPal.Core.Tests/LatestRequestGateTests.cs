using PlayniteCmdPal.Core;
using Xunit;

namespace PlayniteCmdPal.Core.Tests;

public sealed class LatestRequestGateTests
{
    [Fact]
    public void OlderOverlappingRequestCannotPublishAfterNewerRequest()
    {
        var gate = new LatestRequestGate();
        var older = gate.BeginRequest(() => "older query");
        var newer = gate.BeginRequest(() => "newer query");
        string? published = null;

        Assert.False(gate.TryPublish(older.Version, () => published = older.State));
        Assert.Null(published);

        Assert.True(gate.TryPublish(newer.Version, () => published = newer.State));
        Assert.Equal("newer query", published);
    }

    [Fact]
    public void CurrentRequestCanPublishItsResult()
    {
        var gate = new LatestRequestGate();
        var request = gate.BeginRequest(() => 42);
        var published = false;

        Assert.True(gate.TryPublish(request.Version, () => published = true));
        Assert.True(published);
    }
}
