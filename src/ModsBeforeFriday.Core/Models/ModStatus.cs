namespace ModsBeforeFriday.Core.Models;

public sealed record ModStatus(
    AppInfo? AppInfo,
    IReadOnlyList<ModInfo> InstalledMods,
    CoreModsInfo? CoreMods,
    InstallStatus ModloaderInstallStatus);
