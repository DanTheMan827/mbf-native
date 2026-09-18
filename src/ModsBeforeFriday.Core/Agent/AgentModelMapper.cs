using ModsBeforeFriday.Core.Models;

namespace ModsBeforeFriday.Core.Agent;

public static class AgentModelMapper
{
    public static ModInfo ToDomain(this AgentModDto mod) => new(
        mod.Id,
        mod.Name,
        mod.Version,
        mod.GameVersion,
        mod.Description,
        mod.IsEnabled,
        mod.IsCore);

    public static IReadOnlyList<ModInfo> ToDomain(this IReadOnlyList<AgentModDto> mods) =>
        mods.Select(ToDomain).ToArray();

    public static ModStatus ToDomain(this ModStatusAgentResponse response) => new(
        response.AppInfo is null
            ? null
            : new AppInfo(
                response.AppInfo.LoaderInstalled is null ? null : Map(response.AppInfo.LoaderInstalled.Value),
                response.AppInfo.ObbPresent,
                response.AppInfo.Version,
                response.AppInfo.ManifestXml),
        response.InstalledMods.ToDomain(),
        response.CoreMods is null
            ? null
            : new CoreModsInfo(
                response.CoreMods.SupportedVersions,
                response.CoreMods.DowngradeVersions,
                response.CoreMods.IsAwaitingDiff,
                Map(response.CoreMods.CoreModInstallStatus)),
        Map(response.ModloaderInstallStatus));

    public static ImportResult ToDomain(this ImportResultAgentResponse response) => new(
        response.UsedFilename,
        response.Result switch
        {
            ImportedModPayloadDto imported => new ImportedMod(imported.InstalledMods.ToDomain(), imported.ImportedId),
            ImportedFileCopyPayloadDto copy => new ImportedFileCopy(copy.CopiedTo, copy.ModId),
            ImportedSongPayloadDto => new ImportedSong(),
            NonQuestModDetectedPayloadDto => new NonQuestModDetected(),
            _ => throw new InvalidOperationException($"Unsupported import result {response.Result.GetType().Name}."),
        });

    public static AgentLogEntry ToDomain(this LogMessageAgentResponse response) => new(
        DateTimeOffset.Now,
        response.Level switch
        {
            AgentLogLevelDto.Error => AgentLogLevel.Error,
            AgentLogLevelDto.Warn => AgentLogLevel.Warn,
            AgentLogLevelDto.Info => AgentLogLevel.Info,
            AgentLogLevelDto.Debug => AgentLogLevel.Debug,
            AgentLogLevelDto.Trace => AgentLogLevel.Trace,
            _ => AgentLogLevel.Info,
        },
        response.Message);

    private static InstallStatus Map(AgentInstallStatus status) => status switch
    {
        AgentInstallStatus.Ready => InstallStatus.Ready,
        AgentInstallStatus.NeedUpdate => InstallStatus.NeedUpdate,
        AgentInstallStatus.Missing => InstallStatus.Missing,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };

    private static ModLoader Map(AgentModLoader loader) => loader switch
    {
        AgentModLoader.Scotland2 => ModLoader.Scotland2,
        AgentModLoader.QuestLoader => ModLoader.QuestLoader,
        AgentModLoader.Unknown => ModLoader.Unknown,
        _ => ModLoader.Unknown,
    };
}
