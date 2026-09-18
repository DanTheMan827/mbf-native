namespace ModsBeforeFriday.Core.Models;

public sealed record ModInfo(
    string Id,
    string Name,
    string Version,
    string? GameVersion,
    string? Description,
    bool IsEnabled,
    bool IsCore);
