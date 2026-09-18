using ModsBeforeFriday.Core.Models;

namespace ModsBeforeFriday.Core.Utilities;

public static class InstallationAnalyzer
{
    public static InstallationAnalysis Analyze(ModStatus status, bool developerMode = false)
    {
        if (status.AppInfo is null)
        {
            return Empty(InstallationState.GameNotInstalled);
        }

        if (status.CoreMods is null)
        {
            return Empty(InstallationState.DeviceHasNoInternet);
        }

        var supported = status.CoreMods.SupportedVersions;
        var downgradeChoices = status.CoreMods.DowngradeVersions
            .Where(supported.Contains)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(v => v, BeatSaberVersionComparer.Descending)
            .ToArray();

        var sortedSupported = supported
            .Distinct(StringComparer.Ordinal)
            .OrderBy(v => v, BeatSaberVersionComparer.Descending)
            .ToArray();
        var newest = sortedSupported.FirstOrDefault();
        var newerExists = newest is not null && !string.Equals(status.AppInfo.Version, newest, StringComparison.Ordinal);

        if (!developerMode && !supported.Contains(status.AppInfo.Version, StringComparer.Ordinal))
        {
            if (downgradeChoices.Length == 0)
            {
                return new InstallationAnalysis(
                    status.CoreMods.IsAwaitingDiff ? InstallationState.AwaitingDowngradeDiff : InstallationState.UnsupportedVersion,
                    downgradeChoices,
                    null,
                    newerExists,
                    newest);
            }

            if (status.AppInfo.LoaderInstalled is not null)
            {
                return new InstallationAnalysis(
                    InstallationState.UnsupportedAlreadyModded,
                    downgradeChoices,
                    downgradeChoices[0],
                    newerExists,
                    newest);
            }

            if (!status.AppInfo.ObbPresent)
            {
                return new InstallationAnalysis(
                    InstallationState.MissingObb,
                    downgradeChoices,
                    downgradeChoices[0],
                    newerExists,
                    newest);
            }

            return new InstallationAnalysis(
                InstallationState.NeedsPatching,
                downgradeChoices,
                downgradeChoices[0],
                newerExists,
                newest);
        }

        if (!status.AppInfo.ObbPresent)
        {
            return new InstallationAnalysis(InstallationState.MissingObb, downgradeChoices, null, newerExists, newest);
        }

        if (status.AppInfo.LoaderInstalled is ModLoader.Scotland2)
        {
            return new InstallationAnalysis(InstallationState.ModdedReady, downgradeChoices, null, newerExists, newest);
        }

        if (status.AppInfo.LoaderInstalled is not null)
        {
            return new InstallationAnalysis(InstallationState.IncompatibleModLoader, downgradeChoices, null, newerExists, newest);
        }

        return new InstallationAnalysis(InstallationState.NeedsPatching, downgradeChoices, null, newerExists, newest);
    }

    private static InstallationAnalysis Empty(InstallationState state) => new(state, [], null, false, null);
}
