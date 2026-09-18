using System.Text.Json.Serialization;

namespace ModsBeforeFriday.Core.Agent;

public sealed record AgentRequestParameters(string GameId, bool IgnorePackageId);

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(GetModStatusRequest), "GetModStatus")]
[JsonDerivedType(typeof(SetModsEnabledRequest), "SetModsEnabled")]
[JsonDerivedType(typeof(RemoveModRequest), "RemoveMod")]
[JsonDerivedType(typeof(ImportFileRequest), "Import")]
[JsonDerivedType(typeof(ImportUrlRequest), "ImportUrl")]
[JsonDerivedType(typeof(PatchRequest), "Patch")]
[JsonDerivedType(typeof(FixPlayerDataRequest), "FixPlayerData")]
[JsonDerivedType(typeof(GetDowngradedManifestRequest), "GetDowngradedManifest")]
[JsonDerivedType(typeof(QuickFixRequest), "QuickFix")]
public abstract record AgentRequest
{
    public required AgentRequestParameters AgentParameters { get; init; }
}

public sealed record GetModStatusRequest : AgentRequest
{
    public string? OverrideCoreModUrl { get; init; }
}

public sealed record SetModsEnabledRequest : AgentRequest
{
    public required IReadOnlyDictionary<string, bool> Statuses { get; init; }
}

public sealed record RemoveModRequest : AgentRequest
{
    public required string Id { get; init; }
}

public sealed record ImportFileRequest : AgentRequest
{
    public required string FromPath { get; init; }
}

public sealed record ImportUrlRequest : AgentRequest
{
    public required string FromUrl { get; init; }
}

public sealed record PatchRequest : AgentRequest
{
    public string? DowngradeTo { get; init; }
    public required string ManifestMod { get; init; }
    public string? VrSplashPath { get; init; }
    public bool Remodding { get; init; }
    public bool AllowNoCoreMods { get; init; }
    public bool DevicePreV51 { get; init; }
    public string? OverrideCoreModUrl { get; init; }
}

public sealed record FixPlayerDataRequest : AgentRequest;

public sealed record GetDowngradedManifestRequest : AgentRequest
{
    public required string Version { get; init; }
}

public sealed record QuickFixRequest : AgentRequest
{
    public string? OverrideCoreModUrl { get; init; }
    public bool WipeExistingMods { get; init; }
}
