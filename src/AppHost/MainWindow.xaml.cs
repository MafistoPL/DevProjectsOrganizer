using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using AppHost.Persistence;
using AppHost.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Web.WebView2.Core;

namespace AppHost;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private const string DefaultDevUrl = "http://localhost:4200/";
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private AppDbContext? _dbContext;
    private RootStore? _rootStore;
    private ScanCoordinator? _scanCoordinator;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await InitializePersistenceAsync();
        await WebView.EnsureCoreWebView2Async();
        WebView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
        WebView.Source = ResolveUiUri();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _dbContext?.Dispose();
    }

    private async Task InitializePersistenceAsync()
    {
        _dbContext = new AppDbContext(AppDbContext.CreateDefaultOptions());
        await _dbContext.Database.MigrateAsync();
        _rootStore = new RootStore(_dbContext);
        await _rootStore.SeedDefaultsAsync();
        _scanCoordinator = new ScanCoordinator(() => new AppDbContext(AppDbContext.CreateDefaultOptions()));
        _scanCoordinator.ScanEvent += OnScanEvent;
    }

    private async void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        if (_rootStore == null)
        {
            return;
        }

        HostRequest? request;
        try
        {
            request = JsonSerializer.Deserialize<HostRequest>(e.WebMessageAsJson, _jsonOptions);
        }
        catch
        {
            return;
        }

        if (request == null || string.IsNullOrWhiteSpace(request.Type))
        {
            return;
        }

        switch (request.Type)
        {
            case "roots.list":
            {
                var roots = await _rootStore.GetAllAsync();
                SendResponse(request.Id, request.Type, roots);
                break;
            }
            case "roots.add":
            {
                if (!request.Payload.HasValue
                    || !request.Payload.Value.TryGetProperty("path", out var pathElement))
                {
                    SendError(request.Id, request.Type, "Missing root path.");
                    return;
                }

                var path = pathElement.GetString();
                if (string.IsNullOrWhiteSpace(path))
                {
                    SendError(request.Id, request.Type, "Root path cannot be empty.");
                    return;
                }

                var root = await _rootStore.AddAsync(path);
                SendResponse(request.Id, request.Type, root);
                break;
            }
            case "roots.update":
            {
                if (!request.Payload.HasValue)
                {
                    SendError(request.Id, request.Type, "Missing payload.");
                    return;
                }

                var payload = request.Payload.Value;
                if (!payload.TryGetProperty("id", out var idElement))
                {
                    SendError(request.Id, request.Type, "Missing root id.");
                    return;
                }

                if (!payload.TryGetProperty("path", out var pathElement))
                {
                    SendError(request.Id, request.Type, "Missing root path.");
                    return;
                }

                var idValue = idElement.GetString();
                if (!Guid.TryParse(idValue, out var rootId))
                {
                    SendError(request.Id, request.Type, "Invalid root id.");
                    return;
                }

                var path = pathElement.GetString();
                if (string.IsNullOrWhiteSpace(path))
                {
                    SendError(request.Id, request.Type, "Root path cannot be empty.");
                    return;
                }

                try
                {
                    var root = await _rootStore.UpdateAsync(rootId, path);
                    SendResponse(request.Id, request.Type, root);
                }
                catch (Exception ex)
                {
                    SendError(request.Id, request.Type, ex.Message);
                }
                break;
            }
            case "roots.delete":
            {
                if (!request.Payload.HasValue)
                {
                    SendError(request.Id, request.Type, "Missing payload.");
                    return;
                }

                var payload = request.Payload.Value;
                if (!payload.TryGetProperty("id", out var idElement))
                {
                    SendError(request.Id, request.Type, "Missing root id.");
                    return;
                }

                var idValue = idElement.GetString();
                if (!Guid.TryParse(idValue, out var rootId))
                {
                    SendError(request.Id, request.Type, "Invalid root id.");
                    return;
                }

                var deleted = await _rootStore.DeleteAsync(rootId);
                SendResponse(request.Id, request.Type, new { id = rootId, deleted });
                break;
            }
            case "scan.list":
            {
                if (_scanCoordinator == null)
                {
                    SendError(request.Id, request.Type, "Scan coordinator not ready.");
                    return;
                }

                var scans = await _scanCoordinator.ListActiveAsync(CancellationToken.None);
                SendResponse(request.Id, request.Type, scans);
                break;
            }
            case "scan.start":
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
                break;
            }
            case "scan.pause":
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
                break;
            }
            case "scan.resume":
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
                break;
            }
            case "scan.stop":
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
                break;
            }
            default:
            {
                SendError(request.Id, request.Type, $"Unknown message: {request.Type}");
                break;
            }
        }
    }

    private void SendResponse(string id, string type, object? data)
    {
        if (WebView.CoreWebView2 == null)
        {
            return;
        }

        var response = new HostResponse(id, type, true, data, null);
        var json = JsonSerializer.Serialize(response, _jsonOptions);
        WebView.CoreWebView2.PostWebMessageAsJson(json);
    }

    private void SendError(string id, string type, string error)
    {
        if (WebView.CoreWebView2 == null)
        {
            return;
        }

        var response = new HostResponse(id, type, false, null, error);
        var json = JsonSerializer.Serialize(response, _jsonOptions);
        WebView.CoreWebView2.PostWebMessageAsJson(json);
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

    private Uri ResolveUiUri()
    {
        var overrideUrl = Environment.GetEnvironmentVariable("APPHOST_UI_URL");
        if (!string.IsNullOrWhiteSpace(overrideUrl))
        {
            return new Uri(overrideUrl, UriKind.Absolute);
        }

#if DEBUG
        return new Uri(DefaultDevUrl, UriKind.Absolute);
#else
        var distIndex = FindDistIndex();
        if (distIndex != null)
        {
            return new Uri(distIndex, UriKind.Absolute);
        }

        return new Uri(DefaultDevUrl, UriKind.Absolute);
#endif
    }

    private string? FindDistIndex()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var distRoot = Path.Combine(dir.FullName, "ui", "dist");
            if (Directory.Exists(distRoot))
            {
                var index = Directory
                    .GetFiles(distRoot, "index.html", SearchOption.AllDirectories)
                    .FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(index))
                {
                    return index;
                }
            }

            dir = dir.Parent;
        }

        return null;
    }

    private sealed record HostRequest(string Id, string Type, JsonElement? Payload);
    private sealed record HostResponse(string Id, string Type, bool Ok, object? Data, string? Error);
}
