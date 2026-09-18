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

    [Fact]
    public void InstalledModExposesCatalogUpdateMetadata()
    {
        var mod = new ModInfo("example.mod", "Example", "1.0.0", "1.40.0", "Description", true, false);
        var updateMod = new ModCatalogMod(
            "example.mod", "Example", "2.0.0", "https://example.invalid/mod.qmod",
            "https://github.com/example/example", "Author", "https://example.invalid/cover.png", "Scotland2", "Description", false);
        var item = new InstalledModItemViewModel(mod, new ModCatalogEntry(updateMod, AlreadyInstalled: true, NeedsUpdate: true));

        Assert.True(item.HasUpdate);
        Assert.Equal("Update available: 2.0.0", item.UpdateText);
    }

    [Fact]
    public void CatalogItemOnlyOffersBugReportForGithubSources()
    {
        var github = new ModCatalogMod(
            "github.mod", "GitHub", "1.0.0", "https://example.invalid/mod.qmod",
            "https://github.com/example/mod", "Author", null, "Scotland2", "Description", false);
        var other = github with { Id = "other.mod", Source = "https://example.invalid/source" };

        Assert.True(new CatalogModItemViewModel(new ModCatalogEntry(github, false, false)).CanReportBug);
        Assert.False(new CatalogModItemViewModel(new ModCatalogEntry(other, false, false)).CanReportBug);
    }

    [Fact]
    public void AppStateOnlyEnablesModdedNavigationForReadyScotland2Device()
    {
        var state = new ModsBeforeFriday.Application.Services.AppState(new ModsBeforeFriday.Application.Services.AppSettings());
        state.SelectedDevice = new DeviceInfo("serial", "Quest", "Quest 3", DeviceConnectionState.Ready, 12);

        Assert.False(state.IsModdedDeviceConnected);

        state.ModStatus = new ModStatus(
            new AppInfo(ModLoader.Scotland2, true, "1.40.0", ManifestXml),
            [],
            null,
            InstallStatus.Ready);

        Assert.True(state.IsModdedDeviceConnected);
        state.Disconnect();
        Assert.False(state.IsModdedDeviceConnected);
    }

    [Fact]
    public async Task AppStateScopesAgentOperationVisibility()
    {
        var state = new ModsBeforeFriday.Application.Services.AppState(new ModsBeforeFriday.Application.Services.AppSettings());
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var operation = state.RunAgentOperationAsync("Testing agent", async () =>
        {
            entered.SetResult(true);
            await release.Task;
        });

        await entered.Task;
        Assert.True(state.IsAgentOperationActive);
        Assert.Equal("Testing agent", state.AgentOperationTitle);

        release.SetResult(true);
        await operation;
        Assert.False(state.IsAgentOperationActive);
    }
}
