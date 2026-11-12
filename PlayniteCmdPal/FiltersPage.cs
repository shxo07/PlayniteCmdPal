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
/// Filter form using the Command Palette's Adaptive Card controls. Expanded multi-select
/// choice sets render as real checkbox controls and retain the familiar grouped layout.
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
            new
            {
                type = "Input.ChoiceSet",
                id = "installed",
                style = "expanded",
                isMultiSelect = true,
                value = string.Join(",", _state.InstalledStatuses
                    .OrderByDescending(value => value)
                    .Select(value => value ? "installed" : "not-installed")),
                choices = new[]
                {
                    new { title = "Installed", value = "installed" },
                    new { title = "Not installed", value = "not-installed" },
                },
            },
            new { type = "TextBlock", text = "Source", weight = "Bolder", wrap = true },
            new
            {
                type = "Input.ChoiceSet",
                id = "sources",
                style = "expanded",
                isMultiSelect = true,
                value = string.Join(",", _state.SelectedSources),
                choices = _state.Sources.Select(value => new { title = value, value }).ToArray(),
            },
            new { type = "TextBlock", text = "Platform", weight = "Bolder", wrap = true },
            new
            {
                type = "Input.ChoiceSet",
                id = "platforms",
                style = "expanded",
                isMultiSelect = true,
                value = string.Join(",", _state.SelectedPlatforms),
                choices = _state.Platforms.Select(value => new { title = value, value }).ToArray(),
            },
        };

        var actions = new object[]
        {
            new { type = "Action.Submit", title = "Apply", data = new { action = "apply" } },
            new { type = "Action.Submit", title = "Clear status", data = new { action = "clear-status" } },
            new { type = "Action.Submit", title = "Clear sources", data = new { action = "clear-sources" } },
            new { type = "Action.Submit", title = "Clear platforms", data = new { action = "clear-platforms" } },
            new { type = "Action.Submit", title = "Clear all", data = new { action = "clear-all" } },
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

        var action = values["action"]?.GetValue<string>() ?? "apply";
        switch (action)
        {
            case "clear-status":
                _state.ClearInstalledStatuses();
                RefreshForm();
                return CommandResult.KeepOpen();

            case "clear-sources":
                _state.ClearSources();
                RefreshForm();
                return CommandResult.KeepOpen();

            case "clear-platforms":
                _state.ClearPlatforms();
                RefreshForm();
                return CommandResult.KeepOpen();

            case "clear-all":
                _state.ClearAll();
                RefreshForm();
                return CommandResult.KeepOpen();

            case "apply":
                _state.SetInstalledStatuses(Parse(values["installed"]?.ToString())
                    .Select(value => value switch
                    {
                        "installed" => (bool?)true,
                        "not-installed" => false,
                        _ => null,
                    })
                    .Where(value => value.HasValue)
                    .Select(value => value!.Value));
                _state.SetSources(Parse(values["sources"]?.ToString()));
                _state.SetPlatforms(Parse(values["platforms"]?.ToString()));
                _filtersChanged();
                return CommandResult.GoBack();

            default:
                return CommandResult.KeepOpen();
        }
    }

    private void RefreshForm()
    {
        _form = CreateForm();
        RaiseItemsChanged(1);
        _filtersChanged();
    }

    private static string[] Parse(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

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
