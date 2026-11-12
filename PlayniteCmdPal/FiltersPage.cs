using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PlayniteCmdPal.Core;

namespace PlayniteCmdPal;

/// <summary>
/// Keyboard-first filter page. Each filter is an Adaptive Card action, so Enter invokes
/// the focused option and the updated state is applied immediately.
/// </summary>
internal sealed partial class FiltersPage : ContentPage
{
    private readonly CatalogFilterState _state;
    private readonly Action _filtersChanged;
    private FilterFormContent _form;

    public FiltersPage(CatalogFilterState state, Action filtersChanged)
    {
        _state = state;
        _filtersChanged = filtersChanged;

        Name = "Filters";
        Title = "Filters";
        _form = CreateForm();
    }

    public override IContent[] GetContent() => [_form];

    private FilterFormContent CreateForm() => new(this, BuildTemplate());

    private string BuildTemplate()
    {
        var body = new List<object>
        {
            new { type = "TextBlock", text = "Installed status", weight = "Bolder", wrap = true },
            CreateChoiceActions(
                ("Installed", "status", "installed", _state.InstalledStatuses.Contains(true)),
                ("Not installed", "status", "not-installed", _state.InstalledStatuses.Contains(false))),
            new { type = "TextBlock", text = "Source", weight = "Bolder", wrap = true },
            CreateChoiceActions(_state.Sources.Select(source =>
                (source, "source", source, _state.SelectedSources.Contains(source, StringComparer.OrdinalIgnoreCase))).ToArray()),
            new { type = "TextBlock", text = "Platform", weight = "Bolder", wrap = true },
            CreateChoiceActions(_state.Platforms.Select(platform =>
                (platform, "platform", platform, _state.SelectedPlatforms.Contains(platform, StringComparer.OrdinalIgnoreCase))).ToArray()),
            // Visually separate the filter choices from the clear-action row below.
            new { type = "TextBlock", text = " ", spacing = "ExtraLarge", separator = true },
        };

        var actions = new object[]
        {
            new { type = "Action.Submit", title = "Clear status", data = new { action = "clear-status" } },
            new { type = "Action.Submit", title = "Clear sources", data = new { action = "clear-sources" } },
            new { type = "Action.Submit", title = "Clear platforms", data = new { action = "clear-platforms" } },
            new { type = "Action.Submit", title = "Clear all", style = "destructive", data = new { action = "clear-all" } },
        };

        return JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["$schema"] = "http://adaptivecards.io/schemas/adaptive-card.json",
            ["type"] = "AdaptiveCard",
            ["version"] = "1.6",
            ["body"] = body,
            ["actions"] = actions,
        });
    }

    private static object CreateChoiceActions(params (string Title, string Dimension, string Value, bool Selected)[] choices)
    {
        var actions = choices.Select(choice => (object)new Dictionary<string, object>
        {
            ["type"] = "Action.Submit",
            ["title"] = $"{(choice.Selected ? "☑" : "☐")} {choice.Title}",
            ["style"] = choice.Selected ? "positive" : "default",
            ["data"] = new
            {
                action = "toggle",
                dimension = choice.Dimension,
                value = choice.Value,
            },
        }).ToArray();

        return new Dictionary<string, object>
        {
            ["type"] = "ActionSet",
            ["actions"] = actions,
        };
    }

    private CommandResult Submit(string payload)
    {
        JsonObject? values;
        try
        {
            values = JsonNode.Parse(payload)?.AsObject();
        }
        catch (JsonException)
        {
            return CommandResult.KeepOpen();
        }

        if (values is null)
        {
            return CommandResult.KeepOpen();
        }

        var action = values["action"]?.GetValue<string>() ?? string.Empty;
        switch (action)
        {
            case "toggle":
                Toggle(
                    values["dimension"]?.GetValue<string>() ?? string.Empty,
                    values["value"]?.GetValue<string>() ?? string.Empty);
                break;

            case "clear-status":
                _state.ClearInstalledStatuses();
                break;
            case "clear-sources":
                _state.ClearSources();
                break;
            case "clear-platforms":
                _state.ClearPlatforms();
                break;
            case "clear-all":
                _state.ClearAll();
                break;
            default:
                return CommandResult.KeepOpen();
        }

        RefreshForm();
        _filtersChanged();
        return CommandResult.KeepOpen();
    }

    private void Toggle(string dimension, string value)
    {
        switch (dimension)
        {
            case "status":
                var statuses = _state.InstalledStatuses.ToHashSet();
                var status = value == "installed";
                if (!statuses.Add(status))
                {
                    statuses.Remove(status);
                }
                _state.SetInstalledStatuses(statuses);
                break;

            case "source":
                var sources = _state.SelectedSources.ToHashSet(StringComparer.OrdinalIgnoreCase);
                if (!sources.Add(value))
                {
                    sources.Remove(value);
                }
                _state.SetSources(sources);
                break;

            case "platform":
                var platforms = _state.SelectedPlatforms.ToHashSet(StringComparer.OrdinalIgnoreCase);
                if (!platforms.Add(value))
                {
                    platforms.Remove(value);
                }
                _state.SetPlatforms(platforms);
                break;
        }
    }

    private void RefreshForm()
    {
        _form = CreateForm();
        RaiseItemsChanged(1);
    }

    private sealed partial class FilterFormContent : FormContent
    {
        private readonly FiltersPage _page;

        public FilterFormContent(FiltersPage page, string template)
        {
            _page = page;
            TemplateJson = template;
        }

        public override CommandResult SubmitForm(string payload) => _page.Submit(payload);
    }
}
