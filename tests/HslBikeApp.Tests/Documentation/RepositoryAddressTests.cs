namespace HslBikeApp.Tests.Documentation;

/// <summary>
/// Guards documentation against the former <c>TmiKuoste</c> repository owner; the canonical owner is <c>Kuoste</c>.
/// </summary>
public class RepositoryAddressTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    [Theory]
    [InlineData("README.md")]
    [InlineData("CLAUDE.md")]
    [InlineData(".github", "copilot-instructions.md")]
    public void Documentation_DoesNotReferenceFormerRepositoryOwner(params string[] pathParts)
    {
        var content = File.ReadAllText(Path.Combine([RepositoryRoot, .. pathParts]));

        Assert.DoesNotContain("tmikuoste", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Instructions_NameLiveGitHubPagesAddress()
    {
        var content = File.ReadAllText(Path.Combine(RepositoryRoot, ".github", "copilot-instructions.md"));

        Assert.Contains("kuoste.github.io/HslBikeApp/", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Readme_LinksBackendRepositoryUnderCanonicalOwner()
    {
        var content = File.ReadAllText(Path.Combine(RepositoryRoot, "README.md"));

        Assert.Contains("https://github.com/Kuoste/HslBikeDataAggregator", content, StringComparison.Ordinal);
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
