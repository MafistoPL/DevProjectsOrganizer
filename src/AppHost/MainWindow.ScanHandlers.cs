using System;
using System.Text.Json;
using AppHost.Services;

namespace AppHost;

public partial class MainWindow
{
    private async Task HandleScanListAsync(HostRequest request)
    {
        if (_scanCoordinator == null)
        {
            SendError(request.Id, request.Type, "Scan coordinator not ready.");
            return;
        }

        var scans = await _scanCoordinator.ListActiveAsync(CancellationToken.None);
        SendResponse(request.Id, request.Type, scans);
    }

    private async Task HandleScanStartAsync(HostRequest request)
    {
        if (_scanCoordinator == null)
        {
            SendError(request.Id, request.Type, "Scan coordinator not ready.");
            return;
        }

        if (!request.Payload.HasValue)
        {
            SendError(request.Id, request.Type, "Missing payload.");
            return;
        }

        var payload = request.Payload.Value;
        if (!payload.TryGetProperty("mode", out var modeElement))
        {
            SendError(request.Id, request.Type, "Missing scan mode.");
            return;
        }

        var mode = modeElement.GetString() ?? "roots";
        Guid? rootId = null;
        if (payload.TryGetProperty("rootId", out var rootIdElement))
        {
            var idValue = rootIdElement.GetString();
            if (Guid.TryParse(idValue, out var parsed))
            {
                rootId = parsed;
            }
        }

        int? depthLimit = null;
        if (payload.TryGetProperty("depthLimit", out var depthElement)
            && depthElement.TryGetInt32(out var parsedDepth))
        {
            depthLimit = parsedDepth > 0 ? parsedDepth : null;
        }

        try
        {
            var session = await _scanCoordinator.StartAsync(
                new ScanStartRequest(mode, rootId, depthLimit),
                CancellationToken.None);
            SendResponse(request.Id, request.Type, session);
        }
        catch (Exception ex)
        {
            SendError(request.Id, request.Type, ex.Message);
        }
    }

    private async Task HandleScanPauseAsync(HostRequest request)
    {
        if (_scanCoordinator == null)
        {
            SendError(request.Id, request.Type, "Scan coordinator not ready.");
            return;
        }

        if (!TryGetScanId(request.Payload, out var scanId))
        {
            SendError(request.Id, request.Type, "Missing scan id.");
            return;
        }

        await _scanCoordinator.PauseAsync(scanId);
        SendResponse(request.Id, request.Type, new { id = scanId });
    }

    private async Task HandleScanResumeAsync(HostRequest request)
    {
        if (_scanCoordinator == null)
        {
            SendError(request.Id, request.Type, "Scan coordinator not ready.");
            return;
        }

        if (!TryGetScanId(request.Payload, out var scanId))
        {
            SendError(request.Id, request.Type, "Missing scan id.");
            return;
        }

        await _scanCoordinator.ResumeAsync(scanId);
        SendResponse(request.Id, request.Type, new { id = scanId });
    }

    private async Task HandleScanStopAsync(HostRequest request)
    {
        if (_scanCoordinator == null)
        {
            SendError(request.Id, request.Type, "Scan coordinator not ready.");
            return;
        }

        if (!TryGetScanId(request.Payload, out var scanId))
        {
            SendError(request.Id, request.Type, "Missing scan id.");
            return;
        }

        await _scanCoordinator.StopAsync(scanId);
        SendResponse(request.Id, request.Type, new { id = scanId });
    }

    private void OnScanEvent(string type, object? data)
    {
        Dispatcher.Invoke(() => SendEvent(type, data));
    }

    private void SendEvent(string type, object? data)
    {
        if (WebView.CoreWebView2 == null)
        {
            return;
        }

        var message = new { type, data };
        var json = JsonSerializer.Serialize(message, _jsonOptions);
        WebView.CoreWebView2.PostWebMessageAsJson(json);
    }

    private static bool TryGetScanId(JsonElement? payload, out Guid scanId)
    {
        scanId = Guid.Empty;
        if (!payload.HasValue)
        {
            return false;
        }

        var element = payload.Value;
        if (!element.TryGetProperty("id", out var idElement))
        {
            return false;
        }

        var idValue = idElement.GetString();
        return Guid.TryParse(idValue, out scanId);
    }
}
