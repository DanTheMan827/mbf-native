namespace ModsBeforeFriday.Core.Models;

public abstract record ImportPayload;

public sealed record ImportedMod(
    IReadOnlyList<ModInfo> InstalledMods,
    string ImportedId) : ImportPayload;

public sealed record ImportedFileCopy(
    string CopiedTo,
    string ModId) : ImportPayload;

public sealed record ImportedSong : ImportPayload;

public sealed record NonQuestModDetected : ImportPayload;

public sealed record ImportResult(string UsedFilename, ImportPayload Result);
