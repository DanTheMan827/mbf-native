namespace ModsBeforeFriday.Core.Models;

public sealed record PatchResult(
    IReadOnlyList<ModInfo> InstalledMods,
    bool DidRemoveDlc);
