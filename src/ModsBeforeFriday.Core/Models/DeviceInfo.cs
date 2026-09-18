namespace ModsBeforeFriday.Core.Models;

public enum DeviceConnectionState
{
    Unknown,
    Ready,
    Offline,
    Unauthorized,
    Unavailable,
}

public sealed record DeviceInfo(
    string Serial,
    string DisplayName,
    string? Model,
    DeviceConnectionState State,
    int? AndroidVersion = null)
{
    public bool IsReady => State == DeviceConnectionState.Ready;

    public bool IsPreAndroid11 => AndroidVersion is < 11;
}
