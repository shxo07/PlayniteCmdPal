using PlayniteCmdPal.Core;
using Xunit;

namespace PlayniteCmdPal.Core.Tests;

public sealed class PlayniteLibraryRequestCoordinatorTests
{
    [Fact]
    public void StartingANewViewRequestCancelsThePreviousRequestAndInvalidatesItsResult()
    {
        using var requests = new PlayniteLibraryRequestCoordinator();

        var searchRequest = requests.BeginRequest();
        Assert.True(requests.IsCurrent(searchRequest.Version));
        Assert.False(searchRequest.CancellationToken.IsCancellationRequested);

        // A view switch, query change, favorite update, or catalog refresh all begin a new
        // page request. Results and artwork from the previous view must become obsolete.
        var recentlyPlayedRequest = requests.BeginRequest();

        Assert.True(searchRequest.CancellationToken.IsCancellationRequested);
        Assert.False(requests.IsCurrent(searchRequest.Version));
        Assert.True(requests.IsCurrent(recentlyPlayedRequest.Version));
        Assert.False(recentlyPlayedRequest.CancellationToken.IsCancellationRequested);
    }

    [Fact]
    public void LaterRequestInvalidatesEarlierArtworkLease()
    {
        using var requests = new PlayniteLibraryRequestCoordinator();

        var searchArtworkRequest = requests.BeginRequest();
        var favoritesRequest = requests.BeginRequest();

        Assert.False(requests.IsCurrent(searchArtworkRequest.Version));
        Assert.True(searchArtworkRequest.CancellationToken.IsCancellationRequested);
        Assert.True(requests.IsCurrent(favoritesRequest.Version));
    }

    [Fact]
    public void DisposalCancelsCurrentRequestAndRejectsFutureRequests()
    {
        var requests = new PlayniteLibraryRequestCoordinator();
        var request = requests.BeginRequest();

        requests.Dispose();

        Assert.True(request.CancellationToken.IsCancellationRequested);
        Assert.False(requests.IsCurrent(request.Version));
        Assert.Throws<ObjectDisposedException>(() => requests.BeginRequest());
    }

    [Fact]
    public void DisposalIsIdempotent()
    {
        var requests = new PlayniteLibraryRequestCoordinator();

        requests.Dispose();
        requests.Dispose();

        Assert.Throws<ObjectDisposedException>(() => requests.BeginRequest());
    }
}
