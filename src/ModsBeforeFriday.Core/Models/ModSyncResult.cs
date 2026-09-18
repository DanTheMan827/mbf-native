namespace ModsBeforeFriday.Core.Models;

public sealed record ModSyncResult(
    IReadOnlyList<ModInfo> InstalledMods,
    string? Failures);
