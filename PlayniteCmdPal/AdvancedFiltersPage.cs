using System;
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
    private const string StatusInputPrefix = "status:";
    private const string SourceInputPrefix = "source:";
    private const string PlatformInputPrefix = "platform:";

    private readonly CatalogFilterState _state;
    private readonly Action _filtersApplied;

    public AdvancedFiltersForm(CatalogFilterState state, Action filtersApplied)
    {
        _state = state;
        _filtersApplied = filtersApplied;
        RebuildTemplate();
    }

    public override CommandResult SubmitForm(string inputs, string data) =>
        ApplySelections(inputs, ReadActionId(data));

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

    private CommandResult ApplySelections(string inputs, string actionId)
    {
        if (string.Equals(actionId, "clear-all", StringComparison.Ordinal))
        {
            _state.ClearAll();
            _ = System.Threading.Tasks.Task.Run(_filtersApplied);
            return CommandResult.GoBack();
        }

        JsonObject? values;
        try
        {
            values = JsonNode.Parse(inputs)?.AsObject();
        }
        catch (JsonException)
        {
            return CommandResult.KeepOpen();
        }

        if (values is null)
        {
            return CommandResult.KeepOpen();
        }

        var statuses = ReadSelectedValues(values, StatusInputPrefix)
            .Select(value => value switch
            {
                "installed" => (bool?)true,
                "not-installed" => false,
                _ => null,
            })
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .ToArray();
        var sources = ReadSelectedValues(values, SourceInputPrefix);
        var platforms = ReadSelectedValues(values, PlatformInputPrefix);

        if (string.Equals(actionId, "clear-status", StringComparison.Ordinal))
        {
            _state.ClearInstalledStatuses();
        }
        else
        {
            _state.SetInstalledStatuses(statuses);
        }

        if (string.Equals(actionId, "clear-sources", StringComparison.Ordinal))
        {
            _state.ClearSources();
        }
        else
        {
            _state.SetSources(sources);
        }

        if (string.Equals(actionId, "clear-platforms", StringComparison.Ordinal))
        {
            _state.ClearPlatforms();
        }
        else
        {
            _state.SetPlatforms(platforms);
        }

        // Defer page refresh until after the form command has returned its navigation result.
        _ = System.Threading.Tasks.Task.Run(_filtersApplied);
        return CommandResult.GoBack();
    }

    private static string[] ReadSelectedValues(JsonObject inputs, string idPrefix) =>
        inputs
            .Where(pair =>
                pair.Key.StartsWith(idPrefix, StringComparison.Ordinal)
                && string.Equals(pair.Value?.ToString(), "true", StringComparison.OrdinalIgnoreCase))
            .Select(pair => Uri.UnescapeDataString(pair.Key[idPrefix.Length..]))
            .ToArray();

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
        };

        AddToggleSection(
            body,
            "Installed status",
            StatusInputPrefix,
            new[] { ("Installed", "installed"), ("Not installed", "not-installed") },
            _state.InstalledStatuses.Select(value => value ? "installed" : "not-installed"));
        AddToggleSection(
            body,
            "Source",
            SourceInputPrefix,
            _state.Sources.Select(value => (value, Uri.EscapeDataString(value))),
            _state.SelectedSources.Select(Uri.EscapeDataString));
        AddToggleSection(
            body,
            "Platform",
            PlatformInputPrefix,
            _state.Platforms.Select(value => (value, Uri.EscapeDataString(value))),
            _state.SelectedPlatforms.Select(Uri.EscapeDataString));

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

    private static void AddToggleSection(
        JsonArray body,
        string label,
        string idPrefix,
        System.Collections.Generic.IEnumerable<(string Title, string Value)> choices,
        System.Collections.Generic.IEnumerable<string> selected)
    {
        body.Add(new JsonObject
        {
            ["type"] = "TextBlock",
            ["text"] = label,
            ["weight"] = "Bolder",
            ["wrap"] = true,
        });

        var selectedValues = selected.ToHashSet(StringComparer.Ordinal);
        foreach (var (title, value) in choices)
        {
            body.Add(new JsonObject
            {
                ["type"] = "Input.Toggle",
                ["id"] = idPrefix + value,
                ["title"] = title,
                ["value"] = selectedValues.Contains(value) ? "true" : "false",
                ["valueOn"] = "true",
                ["valueOff"] = "false",
                ["wrap"] = true,
            });
        }
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
