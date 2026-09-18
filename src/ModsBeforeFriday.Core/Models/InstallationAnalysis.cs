namespace ModsBeforeFriday.Core.Models;

public enum InstallationState
{
    GameNotInstalled,
    DeviceHasNoInternet,
    AwaitingDowngradeDiff,
    UnsupportedVersion,
    UnsupportedAlreadyModded,
    MissingObb,
    NeedsPatching,
    ModdedReady,
    IncompatibleModLoader,
}

public sealed record InstallationAnalysis(
    InstallationState State,
    IReadOnlyList<string> DowngradeChoices,
    string? RecommendedDowngrade,
    bool NewerModdableVersionAvailable,
    string? NewestModdableVersion);
