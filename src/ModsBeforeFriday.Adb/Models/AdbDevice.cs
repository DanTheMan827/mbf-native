namespace ModsBeforeFriday.Adb.Models;

public sealed record AdbDevice(
    string Serial,
    AdbDeviceState State,
    string? Model,
    string? Product,
    string? Device,
    long? TransportId,
    IReadOnlyDictionary<string, string> Attributes)
{
    public string DisplayName => string.IsNullOrWhiteSpace(Model) ? Serial : $"{Model} ({Serial})";
}
