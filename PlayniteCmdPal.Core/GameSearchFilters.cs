namespace PlayniteCmdPal.Core;

/// <summary>
/// Optional metadata constraints for a library search. Values within a source or platform
/// selection are ORed together; the installed, source, and platform dimensions are ANDed.
/// </summary>
public sealed record GameSearchFilters(
    bool? IsInstalled = null,
    IReadOnlyCollection<string>? Sources = null,
    IReadOnlyCollection<string>? Platforms = null)
{
    internal bool Matches(GameEntry entry)
    {
        if (IsInstalled.HasValue && entry.IsInstalled != IsInstalled)
        {
            return false;
        }

        if (HasValues(Sources) && !ContainsValue(Sources!, entry.Source))
        {
            return false;
        }

        if (HasValues(Platforms) && !ContainsValue(Platforms!, entry.Platform))
        {
            return false;
        }

        return true;
    }

    private static bool HasValues(IReadOnlyCollection<string>? values) =>
        values is not null && values.Any(value => !string.IsNullOrWhiteSpace(value));

    private static bool ContainsValue(IEnumerable<string> values, string? candidate) =>
        !string.IsNullOrWhiteSpace(candidate)
        && values.Any(value => !string.IsNullOrWhiteSpace(value)
            && string.Equals(value.Trim(), candidate.Trim(), StringComparison.OrdinalIgnoreCase));
}
