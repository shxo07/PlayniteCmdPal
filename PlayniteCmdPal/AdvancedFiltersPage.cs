using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PlayniteCmdPal.Core;

namespace PlayniteCmdPal;

internal sealed partial class AdvancedFiltersPage : ContentPage
{
    private readonly AdvancedFiltersForm _form;

    public AdvancedFiltersPage(CatalogFilterState state, Action filtersApplied)
    {
        _form = new AdvancedFiltersForm(state, filtersApplied);
        Name = "Advanced filters";
        Title = "Advanced filters";
        Icon = new IconInfo("\\uE71C");
    }

    public override IContent[] GetContent() => [_form];
}

internal sealed partial class AdvancedFiltersForm : FormContent
{
    private readonly CatalogFilterState _state;
    private readonly Action _filtersApplied;
    private readonly HashSet<string> _selectedStatuses = new(StringComparer.Ordinal);
    private readonly HashSet<string> _selectedSources = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _selectedPlatforms = new(StringComparer.OrdinalIgnoreCase);

    public AdvancedFiltersForm(CatalogFilterState state, Action filtersApplied)
    {
        _state = state;
        _filtersApplied = filtersApplied;

        foreach (var status in state.InstalledStatuses)
        {
            _selectedStatuses.Add(status ? "installed" : "not-installed");
        }

        _selectedSources.UnionWith(state.SelectedSources);
        _selectedPlatforms.UnionWith(state.SelectedPlatforms);
        RebuildTemplate();
    }

    public override CommandResult SubmitForm(string inputs, string data)
    {
        var actionId = ReadActionId(data);

        if (actionId.StartsWith("toggle-status:", StringComparison.Ordinal))
        {
            Toggle(_selectedStatuses, actionId["toggle-status:".Length..]);
            RebuildTemplate();
            return CommandResult.KeepOpen();
        }

        if (actionId.StartsWith("toggle-source:", StringComparison.Ordinal))
        {
            Toggle(_selectedSources, Uri.UnescapeDataString(actionId["toggle-source:".Length..]));
            RebuildTemplate();
            return CommandResult.KeepOpen();
        }

        if (actionId.StartsWith("toggle-platform:", StringComparison.Ordinal))
        {
            Toggle(_selectedPlatforms, Uri.UnescapeDataString(actionId["toggle-platform:".Length..]));
            RebuildTemplate();
            return CommandResult.KeepOpen();
        }

        switch (actionId)
        {
            case "clear-status":
                _selectedStatuses.Clear();
                _state.ClearInstalledStatuses();
                _state.SetSources(_selectedSources);
                _state.SetPlatforms(_selectedPlatforms);
                break;
            case "clear-sources":
                _selectedSources.Clear();
                _state.SetInstalledStatuses(_selectedStatuses.Select(ToInstalledStatus));
                _state.ClearSources();
                _state.SetPlatforms(_selectedPlatforms);
                break;
            case "clear-platforms":
                _selectedPlatforms.Clear();
                _state.SetInstalledStatuses(_selectedStatuses.Select(ToInstalledStatus));
                _state.SetSources(_selectedSources);
                _state.ClearPlatforms();
                break;
            case "clear-all":
                _selectedStatuses.Clear();
                _selectedSources.Clear();
                _selectedPlatforms.Clear();
                _state.ClearAll();
                break;
            case "apply":
            default:
                _state.SetInstalledStatuses(_selectedStatuses.Select(ToInstalledStatus));
                _state.SetSources(_selectedSources);
                _state.SetPlatforms(_selectedPlatforms);
                break;
        }

        _ = System.Threading.Tasks.Task.Run(_filtersApplied);
        return CommandResult.GoBack();
    }

    private static string ReadActionId(string data)
    {
        if (string.IsNullOrWhiteSpace(data))
        {
            return "apply";
        }

        try
        {
            return JsonNode.Parse(data)?["action"]?.ToString() ?? "apply";
        }
        catch (JsonException)
        {
            return "apply";
        }
    }

    private static bool ToInstalledStatus(string value) => value == "installed";

    private static void Toggle<T>(HashSet<T> selected, T value)
    {
        if (!selected.Add(value))
        {
            selected.Remove(value);
        }
    }

    private void RebuildTemplate()
    {
        var body = new JsonArray
        {
            new JsonObject
            {
                ["type"] = "TextBlock",
                ["text"] = "Choose any combination. Values within a section are combined with OR; sections are combined with AND.",
                ["wrap"] = true,
            },
            CreateSectionHeading("Installed status"),
            CreateChoiceAction("toggle-status:installed", "Installed", _selectedStatuses.Contains("installed")),
            CreateChoiceAction("toggle-status:not-installed", "Not installed", _selectedStatuses.Contains("not-installed")),
            CreateSectionHeading("Source"),
        };

        foreach (var source in _state.Sources)
        {
            var encoded = Uri.EscapeDataString(source);
            body.Add(CreateChoiceAction("toggle-source:" + encoded, source, _selectedSources.Contains(source)));
        }

        body.Add(CreateSectionHeading("Platform"));
        foreach (var platform in _state.Platforms)
        {
            var encoded = Uri.EscapeDataString(platform);
            body.Add(CreateChoiceAction("toggle-platform:" + encoded, platform, _selectedPlatforms.Contains(platform)));
        }

        var actions = new JsonArray
        {
            CreateAction("apply", "Apply", "positive"),
            CreateAction("clear-status", "Clear status"),
            CreateAction("clear-sources", "Clear sources"),
            CreateAction("clear-platforms", "Clear platforms"),
            CreateAction("clear-all", "Clear all", "destructive"),
        };

        TemplateJson = new JsonObject
        {
            ["$schema"] = "http://adaptivecards.io/schemas/adaptive-card.json",
            ["type"] = "AdaptiveCard",
            ["version"] = "1.6",
            ["body"] = body,
            ["actions"] = actions,
        }.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
    }

    private static JsonObject CreateSectionHeading(string title) => new()
    {
        ["type"] = "TextBlock",
        ["text"] = title,
        ["weight"] = "Bolder",
        ["wrap"] = true,
    };

    private static JsonObject CreateChoiceAction(string id, string title, bool selected)
    {
        var action = CreateAction(id, (selected ? "☑  " : "☐  ") + title);
        action["style"] = selected ? "positive" : "default";
        return new JsonObject
        {
            ["type"] = "ActionSet",
            ["actions"] = new JsonArray(action),
        };
    }

    private static JsonObject CreateAction(string id, string title, string? style = null)
    {
        var action = new JsonObject
        {
            ["type"] = "Action.Submit",
            ["id"] = id,
            ["title"] = title,
            ["data"] = new JsonObject { ["action"] = id },
        };

        if (style is not null)
        {
            action["style"] = style;
        }

        return action;
    }
}
