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
        Icon = new IconInfo("\uE71C");
    }

    public override IContent[] GetContent() => [_form];
}

internal sealed partial class AdvancedFiltersForm : FormContent
{
    private readonly CatalogFilterState _state;
    private readonly Action _filtersApplied;

    public AdvancedFiltersForm(CatalogFilterState state, Action filtersApplied)
    {
        _state = state;
        _filtersApplied = filtersApplied;
        RebuildTemplate();
    }

    public override CommandResult SubmitForm(string inputs, string data) =>
        ApplySelections(inputs, "apply");

    public override CommandResult SubmitAction(string actionId, string inputs, string data) =>
        ApplySelections(inputs, actionId);

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

        {
            var statuses = ReadValues(values, "installedStatuses")
                .Select(value => value switch
                {
                    "installed" => (bool?)true,
                    "not-installed" => false,
                    _ => null,
                })
                .Where(value => value.HasValue)
                .Select(value => value!.Value)
                .ToArray();
            var sources = ReadValues(values, "sources");
            var platforms = ReadValues(values, "platforms");

            if (!string.Equals(actionId, "clear-status", StringComparison.Ordinal))
            {
                _state.SetInstalledStatuses(statuses);
            }

            if (!string.Equals(actionId, "clear-sources", StringComparison.Ordinal))
            {
                _state.SetSources(sources);
            }

            if (!string.Equals(actionId, "clear-platforms", StringComparison.Ordinal))
            {
                _state.SetPlatforms(platforms);
            }

            if (string.Equals(actionId, "clear-status", StringComparison.Ordinal))
            {
                _state.ClearInstalledStatuses();
            }
            else if (string.Equals(actionId, "clear-sources", StringComparison.Ordinal))
            {
                _state.ClearSources();
            }
            else if (string.Equals(actionId, "clear-platforms", StringComparison.Ordinal))
            {
                _state.ClearPlatforms();
            }
        }

        // Defer page refresh until after the form command has returned its navigation result.
        _ = System.Threading.Tasks.Task.Run(_filtersApplied);
        return CommandResult.GoBack();
    }

    private static string[] ReadValues(JsonObject inputs, string id)
    {
        var raw = inputs[id]?.ToString();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        return raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Uri.UnescapeDataString)
            .ToArray();
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
            CreateChoiceSet(
                "installedStatuses",
                "Installed status",
                new[] { ("Installed", "installed"), ("Not installed", "not-installed") },
                _state.InstalledStatuses.Select(value => value ? "installed" : "not-installed")),
            CreateChoiceSet(
                "sources",
                "Source",
                _state.Sources.Select(value => (value, Uri.EscapeDataString(value))),
                _state.SelectedSources.Select(Uri.EscapeDataString)),
            CreateChoiceSet(
                "platforms",
                "Platform",
                _state.Platforms.Select(value => (value, Uri.EscapeDataString(value))),
                _state.SelectedPlatforms.Select(Uri.EscapeDataString)),
        };

        var actions = new JsonArray
        {
            CreateAction("apply", "Apply"),
            CreateAction("clear-status", "Clear status"),
            CreateAction("clear-sources", "Clear sources"),
            CreateAction("clear-platforms", "Clear platforms"),
            CreateAction("clear-all", "Clear all"),
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

    private static JsonObject CreateChoiceSet(
        string id,
        string label,
        System.Collections.Generic.IEnumerable<(string Title, string Value)> choices,
        System.Collections.Generic.IEnumerable<string> selected)
    {
        var choiceArray = new JsonArray();
        foreach (var (title, value) in choices)
        {
            choiceArray.Add(new JsonObject { ["title"] = title, ["value"] = value });
        }

        return new JsonObject
        {
            ["type"] = "Input.ChoiceSet",
            ["id"] = id,
            ["label"] = label,
            ["style"] = "expanded",
            ["isMultiSelect"] = true,
            ["value"] = string.Join(",", selected),
            ["choices"] = choiceArray,
        };
    }

    private static JsonObject CreateAction(string id, string title) => new()
    {
        ["type"] = "Action.Submit",
        ["id"] = id,
        ["title"] = title,
        ["data"] = new JsonObject { ["action"] = id },
    };
}
