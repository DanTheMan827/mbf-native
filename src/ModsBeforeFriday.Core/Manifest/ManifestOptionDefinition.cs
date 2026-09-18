namespace ModsBeforeFriday.Core.Manifest;

public sealed record ManifestOptionDefinition(
    string Name,
    IReadOnlyList<string> Permissions,
    IReadOnlyList<string> Features,
    IReadOnlyDictionary<string, string>? ApplicationMetadata = null,
    IReadOnlyList<string>? NativeLibraries = null);
