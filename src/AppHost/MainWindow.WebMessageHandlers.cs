using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;

namespace AppHost;

public partial class MainWindow
{
    private Dictionary<string, Func<HostRequest, Task>> CreateMessageHandlers()
    {
        return new(StringComparer.Ordinal)
        {
            ["roots.list"] = HandleRootsListAsync,
            ["roots.add"] = HandleRootsAddAsync,
            ["roots.update"] = HandleRootsUpdateAsync,
            ["roots.delete"] = HandleRootsDeleteAsync,
            ["scan.list"] = HandleScanListAsync,
            ["scan.start"] = HandleScanStartAsync,
            ["scan.pause"] = HandleScanPauseAsync,
            ["scan.resume"] = HandleScanResumeAsync,
            ["scan.stop"] = HandleScanStopAsync,
            ["projects.list"] = HandleProjectsListAsync,
            ["projects.delete"] = HandleProjectsDeleteAsync,
            ["projects.update"] = HandleProjectsUpdateAsync,
            ["projects.attachTag"] = HandleProjectsAttachTagAsync,
            ["projects.detachTag"] = HandleProjectsDetachTagAsync,
            ["projects.runTagHeuristics"] = HandleProjectsRunTagHeuristicsAsync,
            ["projects.runAiTagSuggestions"] = HandleProjectsRunAiTagSuggestionsAsync,
            ["tags.list"] = HandleTagsListAsync,
            ["tags.projects"] = HandleTagsProjectsAsync,
            ["tags.add"] = HandleTagsAddAsync,
            ["tags.update"] = HandleTagsUpdateAsync,
            ["tags.delete"] = HandleTagsDeleteAsync,
            ["tagSuggestions.list"] = HandleTagSuggestionsListAsync,
            ["tagSuggestions.setStatus"] = HandleTagSuggestionsSetStatusAsync,
            ["tagSuggestions.delete"] = HandleTagSuggestionsDeleteAsync,
            ["suggestions.list"] = HandleSuggestionsListAsync,
            ["suggestions.setStatus"] = HandleSuggestionsSetStatusAsync,
            ["suggestions.delete"] = HandleSuggestionsDeleteAsync,
            ["suggestions.exportDebug"] = HandleSuggestionsExportDebugAsync,
            ["suggestions.regressionReport"] = HandleSuggestionsRegressionReportAsync,
            ["suggestions.exportRegressionReport"] = HandleSuggestionsExportRegressionReportAsync,
            ["suggestions.exportArchive"] = HandleSuggestionsExportArchiveAsync,
            ["suggestions.openArchiveFolder"] = HandleSuggestionsOpenArchiveFolderAsync,
            ["suggestions.openPath"] = HandleSuggestionsOpenPathAsync
        };
    }

    private async void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        var request = ParseRequest(e.WebMessageAsJson);
        if (request == null || string.IsNullOrWhiteSpace(request.Type))
        {
            return;
        }

        if (!_messageHandlers.TryGetValue(request.Type, out var handler))
        {
            SendError(request.Id, request.Type, $"Unknown message: {request.Type}");
            return;
        }

        await handler(request);
    }

    private HostRequest? ParseRequest(string messageJson)
    {
        try
        {
            return JsonSerializer.Deserialize<HostRequest>(messageJson, _jsonOptions);
        }
        catch
        {
            return null;
        }
    }
}
