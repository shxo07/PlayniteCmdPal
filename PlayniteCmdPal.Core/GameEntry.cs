namespace PlayniteCmdPal.Core;

/// <summary>
/// Represents a Playnite library entry without depending on Playnite or Command Palette SDK types.
/// </summary>
/// <param name="GameId">The stable Playnite game identifier. This is identity, not display text.</param>
/// <param name="Title">The display title supplied by the catalog.</param>
/// <param name="Source">Optional library source or platform label, when available.</param>
/// <param name="IsInstalled">Optional installation state, when available.</param>
/// <param name="IconReference">Optional transport-provided icon reference; its format is adapter-defined.</param>
/// <param name="Platform">Optional platform label supplied by the catalog transport.</param>
public sealed record GameEntry(
    string GameId,
    string Title,
    string? Source = null,
    bool? IsInstalled = null,
    string? IconReference = null,
    string? Platform = null)
{
    /// <summary>Validates required identity and display fields.</summary>
    /// <exception cref="ArgumentException">Thrown when the game ID or title is null, empty, or whitespace.</exception>
    public GameEntry Validate()
    {
        if (string.IsNullOrWhiteSpace(GameId))
        {
            throw new ArgumentException("A stable Playnite game ID is required.", nameof(GameId));
        }

        if (string.IsNullOrWhiteSpace(Title))
        {
            throw new ArgumentException("A display title is required.", nameof(Title));
        }

        return this;
    }
}
