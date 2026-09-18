namespace ModsBeforeFriday.Adb.Models;

public enum AdbDeviceState
{
    Unknown,
    Device,
    Offline,
    Unauthorized,
    NoPermissions,
    Recovery,
    Sideload,
    Bootloader,
}
