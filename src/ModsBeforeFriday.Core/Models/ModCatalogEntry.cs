namespace ModsBeforeFriday.Core.Models;

public sealed record ModCatalogMod(
    string Id,
    string Name,
    string Version,
    string Download,
    string Source,
    string Author,
    string? Cover,
    string Modloader,
    string Description,
    bool IsGlobal);

public sealed record ModCatalogEntry(
    ModCatalogMod Mod,
    bool AlreadyInstalled,
    bool NeedsUpdate);
