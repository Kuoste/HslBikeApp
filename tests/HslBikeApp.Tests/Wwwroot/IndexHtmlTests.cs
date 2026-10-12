namespace HslBikeApp.Tests.Wwwroot;

/// <summary>
/// Guards the backend warm-up request issued from <c>wwwroot/index.html</c> before the .NET runtime boots.
/// </summary>
public class IndexHtmlTests
{
    private static readonly string IndexHtml = File.ReadAllText(
        Path.Combine(FindRepositoryRoot(), "src", "HslBikeApp", "wwwroot", "index.html"));

    [Fact]
    public void IndexHtml_WarmsUpBackendUsingConfiguredBaseUrl()
    {
        Assert.Contains("fetch('appsettings.json')", IndexHtml);
        Assert.Contains("settings.AggregatorBaseUrl", IndexHtml);
        Assert.Contains("'/api/stations'", IndexHtml);
    }

    [Fact]
    public void IndexHtml_WarmUpRequestAvoidsCorsPreflight()
    {
        // A custom header (such as X-App-Source) would trigger a preflight; no-cors forbids them.
        Assert.Contains("mode: 'no-cors'", IndexHtml);
    }

    [Fact]
    public void IndexHtml_WarmUpScriptRunsBeforeBlazorStarts()
    {
        Assert.True(
            IndexHtml.IndexOf("fetch('appsettings.json')", StringComparison.Ordinal) <
            IndexHtml.IndexOf("_framework/blazor.webassembly.js", StringComparison.Ordinal));
    }

    [Fact]
    public void IndexHtml_DoesNotHardcodeBackendHost()
    {
        Assert.DoesNotContain("azure-api.net", IndexHtml);
        Assert.DoesNotContain("azurewebsites.net", IndexHtml);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "HslBikeApp.slnx")))
            directory = directory.Parent;

        return directory?.FullName
            ?? throw new DirectoryNotFoundException("Could not locate the repository root (HslBikeApp.slnx).");
    }
}
