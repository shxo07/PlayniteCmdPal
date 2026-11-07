using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PlayniteCmdPal.Core;

namespace PlayniteCmdPal;

/// <summary>
/// Exposes independent, keyboard-selectable catalog filters. CmdPal keeps the active filter IDs;
/// the page translates them into the SDK-independent search filter model.
/// </summary>
internal sealed partial class PlayniteCatalogFilters : Filters
{
    private static readonly string AllGamesId = "all-games";
    private static readonly string InstalledId = "installed";
    private static readonly string NotInstalledId = "not-installed";
    private string[] _sources = [];
    private string[] _platforms = [];

    public PlayniteCatalogFilters()
    {
        // Start unfiltered; users can explicitly choose a status or metadata filter.
        CurrentFilterId = AllGamesId;
    }

    public override IFilterItem[] GetFilters()
    {
        var filters = new List<IFilterItem>
        {
            new Filter { Id = AllGamesId, Name = "All games" },
            new Separator(),
            new Filter { Id = InstalledId, Name = "Installed" },
            new Filter { Id = NotInstalledId, Name = "Not installed" },
        };

        if (_sources.Length > 0)
        {
            filters.Add(new Separator());
            filters.AddRange(_sources.Select(source => (IFilterItem)new Filter
            {
                Id = Encode("source", source),
                Name = $"Source: {source}",
            }));
        }

        if (_platforms.Length > 0)
        {
            filters.Add(new Separator());
            filters.AddRange(_platforms.Select(platform => (IFilterItem)new Filter
            {
                Id = Encode("platform", platform),
                Name = $"Platform: {platform}",
            }));
        }

        return filters.ToArray();
    }

    public void UpdateCatalog(IEnumerable<PlayniteCmdPal.Core.GameEntry> games)
    {
        _sources = games
            .Select(game => game.Source?.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ThenBy(value => value, StringComparer.Ordinal)
            .ToArray();

        _platforms = games
            .Select(game => game.Platform?.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ThenBy(value => value, StringComparer.Ordinal)
            .ToArray();
    }

    public GameSearchFilters ToSearchFilters()
    {
        var selected = CurrentFilterId is { Length: > 0 } currentFilterId
            && !string.Equals(currentFilterId, AllGamesId, StringComparison.Ordinal)
            ? new[] { currentFilterId }
            : Array.Empty<string>();
        bool? installed = null;
        if (selected.Contains(InstalledId, StringComparer.Ordinal))
        {
            installed = true;
        }
        else if (selected.Contains(NotInstalledId, StringComparer.Ordinal))
        {
            installed = false;
        }

        return new GameSearchFilters(
            IsInstalled: installed,
            Sources: DecodeValues(selected, "source"),
            Platforms: DecodeValues(selected, "platform"));
    }

    private static string Encode(string dimension, string value) =>
        $"{dimension}:{Uri.EscapeDataString(value)}";

    private static IReadOnlyList<string> DecodeValues(IEnumerable<string> ids, string dimension) =>
        ids.Where(id => id.StartsWith(dimension + ":", StringComparison.Ordinal))
            .Select(id => Uri.UnescapeDataString(id[(dimension.Length + 1)..]))
            .ToArray();
}
