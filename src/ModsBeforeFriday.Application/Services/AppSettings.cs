namespace ModsBeforeFriday.Application.Services;

public sealed class AppSettings
{
    public bool DeveloperMode { get; set; }

    public string? CoreModOverrideUrl { get; set; }

    public string GameId { get; set; } = "com.beatgames.beatsaber";

    public bool IgnorePackageId { get; set; }

    public string AdbHost { get; set; } = "127.0.0.1";

    public int AdbPort { get; set; } = 5037;

    public string? AdbExecutablePath { get; set; }
}
