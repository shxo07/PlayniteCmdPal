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
        // Only explicit, well-formed actions may change applied filter state.
        if (!TryReadActionId(data, out var actionId)
            || !TryReadSelections(inputs))
        {
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
                _state.SetInstalledStatuses(_selectedStatuses.Select(ToInstalledStatus));
                _state.SetSources(_selectedSources);
                _state.SetPlatforms(_selectedPlatforms);
                break;
            default:
                // Do not apply filters or close the page for an unknown action.
                return CommandResult.KeepOpen();
        }

        // Keep the refresh deferred until after the form command has returned its
        // navigation result, matching the existing Command Palette lifecycle.
        _ = System.Threading.Tasks.Task.Run(_filtersApplied);
        return CommandResult.GoBack();
    }

    private bool TryReadSelections(string inputs)
    {
        if (string.IsNullOrWhiteSpace(inputs))
        {
            return false;
        }

        try
        {
            if (JsonNode.Parse(inputs) is not JsonObject values)
            {
                return false;
            }

            var statuses = new HashSet<string>(StringComparer.Ordinal);
            var sources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var platforms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (!TryReadToggle(values, "status-installed", out var installed)
                || !TryReadToggle(values, "status-not-installed", out var notInstalled))
            {
                return false;
            }

            if (installed)
            {
                statuses.Add("installed");
            }

            if (notInstalled)
            {
                statuses.Add("not-installed");
            }

            foreach (var source in _state.Sources)
            {
                if (!TryReadToggle(values, GetSourceInputId(source), out var selected))
                {
                    return false;
                }

                if (selected)
                {
                    sources.Add(source);
                }
            }

            foreach (var platform in _state.Platforms)
            {
                if (!TryReadToggle(values, GetPlatformInputId(platform), out var selected))
                {
                    return false;
                }

                if (selected)
                {
                    platforms.Add(platform);
                }
            }

            _selectedStatuses.Clear();
            _selectedStatuses.UnionWith(statuses);
            _selectedSources.Clear();
            _selectedSources.UnionWith(sources);
            _selectedPlatforms.Clear();
            _selectedPlatforms.UnionWith(platforms);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static bool TryReadToggle(JsonObject values, string id, out bool selected)
    {
        selected = false;
        if (values[id] is not JsonValue value)
        {
            return false;
        }

        if (value.TryGetValue<string>(out var stringValue))
        {
            if (string.Equals(stringValue, "true", StringComparison.OrdinalIgnoreCase))
            {
                selected = true;
                return true;
            }

            if (string.Equals(stringValue, "false", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        return value.TryGetValue(out selected);
    }

    private static bool TryReadActionId(string data, out string actionId)
    {
        actionId = string.Empty;
        if (string.IsNullOrWhiteSpace(data))
        {
            return false;
        }

        try
        {
            if (JsonNode.Parse(data) is not JsonObject values
                || values["action"] is not JsonValue actionValue
                || !actionValue.TryGetValue<string>(out var parsedAction)
                || string.IsNullOrWhiteSpace(parsedAction))
            {
                return false;
            }

            actionId = parsedAction;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static bool ToInstalledStatus(string value) => value == "installed";

    private static string GetSourceInputId(string source) => "source-" + Uri.EscapeDataString(source);

    private static string GetPlatformInputId(string platform) => "platform-" + Uri.EscapeDataString(platform);

    private void RebuildTemplate()
    {
        var body = new JsonArray
        {
            CreateSectionHeading("Installed status"),
            CreateToggleInput("status-installed", "Installed", _selectedStatuses.Contains("installed")),
            CreateToggleInput("status-not-installed", "Not installed", _selectedStatuses.Contains("not-installed")),
            CreateSectionHeading("Source"),
        };

        foreach (var source in _state.Sources)
        {
            body.Add(CreateToggleInput(GetSourceInputId(source), source, _selectedSources.Contains(source)));
        }

        body.Add(CreateSectionHeading("Platform"));
        foreach (var platform in _state.Platforms)
        {
            body.Add(CreateToggleInput(GetPlatformInputId(platform), platform, _selectedPlatforms.Contains(platform)));
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

    private static JsonObject CreateToggleInput(string id, string title, bool selected) => new()
    {
        ["type"] = "Input.Toggle",
        ["id"] = id,
        ["title"] = title,
        ["value"] = selected ? "true" : "false",
        ["valueOn"] = "true",
        ["valueOff"] = "false",
        ["wrap"] = true,
    };

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
