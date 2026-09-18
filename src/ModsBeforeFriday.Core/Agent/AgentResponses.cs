using System.Text.Json.Serialization;

namespace ModsBeforeFriday.Core.Agent;

public enum AgentInstallStatus
{
    Ready,
    NeedUpdate,
    Missing,
}

public enum AgentModLoader
{
    Scotland2,
    QuestLoader,
    Unknown,
}

public enum AgentLogLevelDto
{
    Error,
    Warn,
    Info,
    Debug,
    Trace,
}

public sealed record AgentModDto(
    string Id,
    string Name,
    string Version,
    string? GameVersion,
    string? Description,
    bool IsEnabled,
    bool IsCore);

public sealed record AgentAppInfoDto(
    AgentModLoader? LoaderInstalled,
    bool ObbPresent,
    string Version,
    string ManifestXml);

public sealed record AgentCoreModsInfoDto(
    IReadOnlyList<string> SupportedVersions,
    IReadOnlyList<string> DowngradeVersions,
    bool IsAwaitingDiff,
    AgentInstallStatus CoreModInstallStatus);

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(LogMessageAgentResponse), "LogMsg")]
[JsonDerivedType(typeof(ModStatusAgentResponse), "ModStatus")]
[JsonDerivedType(typeof(ModsAgentResponse), "Mods")]
[JsonDerivedType(typeof(ModSyncResultAgentResponse), "ModSyncResult")]
[JsonDerivedType(typeof(PatchedAgentResponse), "Patched")]
[JsonDerivedType(typeof(ImportResultAgentResponse), "ImportResult")]
[JsonDerivedType(typeof(FixedPlayerDataAgentResponse), "FixedPlayerData")]
[JsonDerivedType(typeof(DowngradedManifestAgentResponse), "DowngradedManifest")]
public abstract record AgentResponse;

public sealed record LogMessageAgentResponse(string Message, AgentLogLevelDto Level) : AgentResponse;

public sealed record ModStatusAgentResponse(
    AgentAppInfoDto? AppInfo,
    IReadOnlyList<AgentModDto> InstalledMods,
    AgentCoreModsInfoDto? CoreMods,
    AgentInstallStatus ModloaderInstallStatus) : AgentResponse;

public sealed record ModsAgentResponse(IReadOnlyList<AgentModDto> InstalledMods) : AgentResponse;

public sealed record ModSyncResultAgentResponse(
    IReadOnlyList<AgentModDto> InstalledMods,
    string? Failures) : AgentResponse;

public sealed record PatchedAgentResponse(
    IReadOnlyList<AgentModDto> InstalledMods,
    bool DidRemoveDlc) : AgentResponse;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(ImportedModPayloadDto), "ImportedMod")]
[JsonDerivedType(typeof(ImportedFileCopyPayloadDto), "ImportedFileCopy")]
[JsonDerivedType(typeof(ImportedSongPayloadDto), "ImportedSong")]
[JsonDerivedType(typeof(NonQuestModDetectedPayloadDto), "NonQuestModDetected")]
public abstract record ImportPayloadDto;

public sealed record ImportedModPayloadDto(
    IReadOnlyList<AgentModDto> InstalledMods,
    string ImportedId) : ImportPayloadDto;

public sealed record ImportedFileCopyPayloadDto(string CopiedTo, string ModId) : ImportPayloadDto;

public sealed record ImportedSongPayloadDto : ImportPayloadDto;

public sealed record NonQuestModDetectedPayloadDto : ImportPayloadDto;

public sealed record ImportResultAgentResponse(
    ImportPayloadDto Result,
    string UsedFilename) : AgentResponse;

public sealed record FixedPlayerDataAgentResponse(bool Existed) : AgentResponse;

public sealed record DowngradedManifestAgentResponse(string ManifestXml) : AgentResponse;
