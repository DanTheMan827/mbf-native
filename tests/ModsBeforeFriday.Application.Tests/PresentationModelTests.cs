using ModsBeforeFriday.Application.ViewModels;
using ModsBeforeFriday.Core.Manifest;
using ModsBeforeFriday.Core.Models;
using Xunit;

namespace ModsBeforeFriday.Application.Tests;

public sealed class PresentationModelTests
{
    private const string ManifestXml = """
        <manifest xmlns:android="http://schemas.android.com/apk/res/android">
          <application android:label="Beat Saber" />
        </manifest>
        """;

    [Fact]
    public void ManifestOptionToggleMutatesUiIndependentManifest()
    {
        var manifest = new AndroidManifestDocument(ManifestXml);
        var microphone = ManifestOptions.Displayed.Single(option => option.Name == "Microphone Access");
        var item = new ManifestOptionItemViewModel(manifest, microphone);

        Assert.False(item.IsEnabled);
        item.IsEnabled = true;

        Assert.Contains("android.permission.RECORD_AUDIO", manifest.Permissions);
        Assert.True(item.IsEnabled);
    }

    [Fact]
    public void InstalledModTracksPendingEnableChange()
    {
        var item = new InstalledModItemViewModel(new ModInfo(
            "example.mod", "Example", "1.0.0", "1.40.0", null, true, false));

        Assert.False(item.HasPendingChange);
        item.IsEnabled = false;
        Assert.True(item.HasPendingChange);
        item.IsEnabled = true;
        Assert.False(item.HasPendingChange);
    }

    [Fact]
    public void CatalogItemDefaultsUpdatesToSelected()
    {
        var mod = new ModCatalogMod(
            "example.mod", "Example", "2.0.0", "https://example.invalid/mod.qmod",
            "https://example.invalid/source", "Author", null, "Scotland2", "Description", false);

        var update = new CatalogModItemViewModel(new ModCatalogEntry(mod, AlreadyInstalled: true, NeedsUpdate: true));
        var install = new CatalogModItemViewModel(new ModCatalogEntry(mod, AlreadyInstalled: false, NeedsUpdate: false));

        Assert.True(update.IsSelected);
        Assert.False(install.IsSelected);
    }
}
