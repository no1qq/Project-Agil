using System.Runtime.CompilerServices;
using ProjectAgil.Services;
using Xunit;

namespace ProjectAgil.Tests;

public sealed class UpdateTests
{
    [Theory]
    [InlineData("b1", 1)]
    [InlineData("b2", 2)]
    [InlineData("b1391", 1391)]
    [InlineData("B12", 12)]
    [InlineData(" b7 ", 7)]
    public void ABuildTagParsesToItsNumber(string tag, int expected) =>
        Assert.Equal(expected, UpdateService.ParseBuild(tag));

    [Theory]
    [InlineData("1391")]
    [InlineData("v1.0.0")]
    [InlineData("b1.2")]
    [InlineData("build12")]
    [InlineData("b")]
    [InlineData("")]
    [InlineData(null)]
    public void AnythingElseParsesToZero(string? tag) => Assert.Equal(0, UpdateService.ParseBuild(tag));

    [Fact]
    public void TheSetupAssetNameMatchesWhatTheInstallerScriptWrites()
    {
        var iss = File.ReadAllText(Path.Combine(RepoRoot(), "build", "ProjectAgil.iss"));
        var expected = Path.GetFileNameWithoutExtension(UpdateService.SetupAsset);

        Assert.Contains($"OutputBaseFilename={expected}", iss, StringComparison.Ordinal);
    }

    [Fact]
    public void ThePortableAssetNameMatchesWhatTheBuildScriptWrites()
    {
        var script = File.ReadAllText(Path.Combine(RepoRoot(), "build", "build-portable.bat"));

        Assert.Contains($"dist\\{UpdateService.PortableAsset}", script, StringComparison.Ordinal);
    }

    [Fact]
    public void TheBuildNumberIsTheMajorPartOfTheAssemblyVersion()
    {
        var version = typeof(UpdateService).Assembly.GetName().Version;

        Assert.NotNull(version);
        Assert.Equal(0, version!.Minor);
        Assert.Equal(0, version.Build);
    }

    private static string RepoRoot([CallerFilePath] string path = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, "..", ".."));
}
