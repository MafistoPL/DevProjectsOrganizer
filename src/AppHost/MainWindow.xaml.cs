using System;
using System.Collections.Generic;
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
    private readonly Dictionary<string, Func<HostRequest, Task>> _messageHandlers;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Closed += OnClosed;
        _messageHandlers = CreateMessageHandlers();
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
        if (_scanCoordinator != null)
        {
            try
            {
                _scanCoordinator.ArchiveCompletedAsync(CancellationToken.None).GetAwaiter().GetResult();
            }
            catch
            {
                // Best-effort cleanup during shutdown.
            }
        }

        _dbContext?.Dispose();
    }

    private async Task InitializePersistenceAsync()
    {
        _dbContext = new AppDbContext(AppDbContext.CreateDefaultOptions());
        await _dbContext.Database.MigrateAsync();
        _rootStore = new RootStore(_dbContext);
        await _rootStore.SeedDefaultsAsync();
        var tagStore = new TagStore(_dbContext);
        await tagStore.SeedDefaultTagsAsync();
        _scanCoordinator = new ScanCoordinator(() => new AppDbContext(AppDbContext.CreateDefaultOptions()));
        _scanCoordinator.ScanEvent += OnScanEvent;
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
