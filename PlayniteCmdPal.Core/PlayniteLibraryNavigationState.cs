namespace PlayniteCmdPal.Core;

/// <summary>A consistent snapshot of the unified library page's navigation state.</summary>
public sealed record PlayniteLibraryNavigationSnapshot(
    PlayniteLibraryViewMode ViewMode,
    string SearchText);

/// <summary>
/// Owns the selected library view and shared search text. Switching views deliberately
/// changes only the view mode, preserving the current query for the destination view.
/// </summary>
public sealed class PlayniteLibraryNavigationState
{
    private readonly object _gate = new();
    private PlayniteLibraryViewMode _viewMode = PlayniteLibraryViewMode.Search;
    private string _searchText = string.Empty;

    public PlayniteLibraryNavigationSnapshot Capture()
    {
        lock (_gate)
            return new PlayniteLibraryNavigationSnapshot(_viewMode, _searchText);
    }

    public void SetSearchText(string searchText)
    {
        ArgumentNullException.ThrowIfNull(searchText);
        lock (_gate)
            _searchText = searchText;
    }

    public bool SwitchView(PlayniteLibraryViewMode viewMode)
    {
        if (!Enum.IsDefined(viewMode))
            throw new ArgumentOutOfRangeException(nameof(viewMode));

        lock (_gate)
        {
            if (_viewMode == viewMode)
                return false;

            _viewMode = viewMode;
            return true;
        }
    }
}
