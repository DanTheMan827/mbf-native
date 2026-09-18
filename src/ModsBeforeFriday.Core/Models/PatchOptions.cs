namespace ModsBeforeFriday.Core.Models;

public sealed record PatchOptions(
    string ManifestXml,
    string? DowngradeTo = null,
    bool Remodding = false,
    bool AllowNoCoreMods = false,
    bool DevicePreV51 = false,
    string? SplashScreenPath = null,
    string? OverrideCoreModUrl = null);
