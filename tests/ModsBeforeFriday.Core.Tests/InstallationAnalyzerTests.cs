using ModsBeforeFriday.Core.Models;
using ModsBeforeFriday.Core.Utilities;
using Xunit;

namespace ModsBeforeFriday.Core.Tests;

public sealed class InstallationAnalyzerTests
{
    [Fact]
    public void UnsupportedVanillaVersionWithSupportedDiffRecommendsNewestDowngrade()
    {
        var status = Status("1.40.0", loader: null, supported: ["1.37.0", "1.39.1"], downgrades: ["1.37.0", "1.39.1"]);

        var analysis = InstallationAnalyzer.Analyze(status);

        Assert.Equal(InstallationState.NeedsPatching, analysis.State);
        Assert.Equal("1.39.1", analysis.RecommendedDowngrade);
    }

    [Fact]
    public void SupportedScotlandInstallIsReady()
    {
        var status = Status("1.39.1", ModLoader.Scotland2, ["1.39.1"], []);

        var analysis = InstallationAnalyzer.Analyze(status);

        Assert.Equal(InstallationState.ModdedReady, analysis.State);
    }

    private static ModStatus Status(string version, ModLoader? loader, string[] supported, string[] downgrades) => new(
        new AppInfo(loader, true, version, MinimalManifest),
        [],
        new CoreModsInfo(supported, downgrades, false, InstallStatus.Ready),
        InstallStatus.Ready);

    private const string MinimalManifest = "<manifest xmlns:android=\"http://schemas.android.com/apk/res/android\"><application /></manifest>";
}
