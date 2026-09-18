namespace ModsBeforeFriday.Backend.Configuration;

public sealed record BackendOptions
{
    public string GameId { get; init; } = "com.beatgames.beatsaber";

    public bool IgnorePackageId { get; init; }

    public required string AgentBinaryPath { get; init; }

    public string? AdbExecutablePath { get; init; }

    public bool StartLocalAdbServerIfUnavailable { get; init; } = true;

    public Uri ModRepositoryBaseUri { get; init; } = new("https://mods.bsquest.xyz/");
}
