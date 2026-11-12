using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PlayniteCmdPal.Core;

namespace PlayniteCmdPal;

/// <summary>
/// Adaptive Card filter picker. Each choice is an Action.Submit button so mouse
/// activation and keyboard Enter both invoke the same immediate filter update.
/// </summary>
internal sealed partial class FiltersPage : ContentPage
{
    private readonly CatalogFilterState _state;
    private readonly Action _filtersChanged;
    private readonly SynchronizationContext? _uiContext;
    private FilterFormContent _form;

    public FiltersPage(CatalogFilterState state, Action filtersChanged)
    {
        _state = state;
        _filtersChanged = filtersChanged;
        _uiContext = SynchronizationContext.Current;

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
            // Extra vertical separation requested between the final platform choice
            // and the clear-action row.
            new { type = "TextBlock", text = " ", spacing = "ExtraLarge", separator = true },
        };

        var actions = new object[]
        {
            new { type = "Action.Submit", title = "Clear status", data = new { action = "clear-status" } },
            new { type = "Action.Submit", title = "Clear sources", data = new { action = "clear-sources" } },
            new { type = "Action.Submit", title = "Clear platforms", data = new { action = "clear-platforms" } },
            new { type = "Action.Submit", title = "Clear all", style = "destructive", data = new { action = "clear-all" } },
        };

        return System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, object>
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

    private CommandResult Submit(string data)
    {
        JsonObject? values;
        try
        {
            values = JsonNode.Parse(data)?.AsObject();
        }
        catch (System.Text.Json.JsonException)
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
                ScheduleRefresh();
                break;
            case "clear-sources":
                _state.ClearSources();
                ScheduleRefresh();
                break;
            case "clear-platforms":
                _state.ClearPlatforms();
                ScheduleRefresh();
                break;
            case "clear-all":
                _state.ClearAll();
                ScheduleRefresh();
                break;
            default:
                return CommandResult.KeepOpen();
        }

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
            default:
                return;
        }

        ScheduleRefresh();
    }

    /// <summary>
    /// Defer replacing the form until the host has completed dispatching its submit
    /// event. Then refresh both the card and the parent catalog results.
    /// </summary>
    private async void ScheduleRefresh()
    {
        await Task.Delay(50);

        void Refresh()
        {
            _form = CreateForm();
            RaiseItemsChanged(1);
            _filtersChanged();
        }

        if (_uiContext is { } context &&
            !ReferenceEquals(context, SynchronizationContext.Current))
        {
            context.Post(_ => Refresh(), null);
        }
        else
        {
            Refresh();
        }
    }

    private sealed partial class FilterFormContent : FormContent
    {
        private readonly FiltersPage _page;

        public FilterFormContent(FiltersPage page, string template)
        {
            _page = page;
            TemplateJson = template;
        }

        // Command Palette supplies Action.Submit's custom data separately from
        // ordinary form inputs. Parse the data argument, not the inputs payload.
        public override CommandResult SubmitForm(string inputs, string data) =>
            _page.Submit(data);
    }
}
