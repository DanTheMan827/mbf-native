namespace ModsBeforeFriday.Core.Models;

public sealed record CoreModsInfo(
    IReadOnlyList<string> SupportedVersions,
    IReadOnlyList<string> DowngradeVersions,
    bool IsAwaitingDiff,
    InstallStatus CoreModInstallStatus);
