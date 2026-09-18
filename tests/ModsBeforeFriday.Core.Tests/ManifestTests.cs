using ModsBeforeFriday.Core.Manifest;
using Xunit;

namespace ModsBeforeFriday.Core.Tests;

public sealed class ManifestTests
{
    private const string MinimalManifest = """
        <manifest xmlns:android="http://schemas.android.com/apk/res/android" package="com.beatgames.beatsaber">
          <application android:debuggable="false" />
        </manifest>
        """;

    [Fact]
    public void ApplyPatchingDefaultsMatchesWebsiteDefaults()
    {
        var manifest = new AndroidManifestDocument(MinimalManifest);

        manifest.ApplyPatchingDefaults();

        Assert.Contains("android.permission.MANAGE_EXTERNAL_STORAGE", manifest.Permissions);
        Assert.Contains("android.permission.WRITE_EXTERNAL_STORAGE", manifest.Permissions);
        Assert.Contains("android.permission.READ_EXTERNAL_STORAGE", manifest.Permissions);
        Assert.Equal("quest|quest2", manifest.Metadata["com.oculus.supportedDevices"]);
        Assert.Contains("debuggable=\"true\"", manifest.ToXml());
        Assert.Contains("hardwareAccelerated=\"true\"", manifest.ToXml());
        Assert.Contains("requestLegacyExternalStorage=\"true\"", manifest.ToXml());
    }

    [Fact]
    public void ManifestOptionRoundTripsAllConstituentFeatures()
    {
        var manifest = new AndroidManifestDocument(MinimalManifest);
        var handTracking = Assert.Single(ManifestOptions.Displayed, option => option.Name == "Hand tracking");

        manifest.SetOption(handTracking, true);
        Assert.True(manifest.IsOptionEnabled(handTracking));
        Assert.Contains("com.oculus.permission.HAND_TRACKING", manifest.Permissions);
        Assert.Contains("oculus.software.handtracking", manifest.Features);
        Assert.Equal("MAX", manifest.Metadata["com.oculus.handtracking.frequency"]);

        manifest.SetOption(handTracking, false);
        Assert.False(manifest.IsOptionEnabled(handTracking));
    }

    [Fact]
    public void InvalidManifestIsRejected()
    {
        Assert.Throws<ArgumentException>(() => new AndroidManifestDocument("<not-a-manifest />"));
    }
}
