namespace ModsBeforeFriday.Core.Models;

public sealed record AppInfo(
    ModLoader? LoaderInstalled,
    bool ObbPresent,
    string Version,
    string ManifestXml);
