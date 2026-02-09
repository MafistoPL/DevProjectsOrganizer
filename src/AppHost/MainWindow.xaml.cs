using System;
using System.IO;
using System.Linq;
using System.Windows;

namespace AppHost;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private const string DefaultDevUrl = "http://localhost:4200/";

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await WebView.EnsureCoreWebView2Async();
        WebView.Source = ResolveUiUri();
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
}
