namespace ModsBeforeFriday.Core.Manifest;

public static class ManifestOptions
{
    public static IReadOnlyList<ManifestOptionDefinition> Displayed { get; } =
    [
        new("Microphone Access", ["android.permission.RECORD_AUDIO"], []),
        new("Passthrough to headset cameras", [], ["com.oculus.feature.PASSTHROUGH"]),
        new("Body tracking", ["com.oculus.permission.BODY_TRACKING"], ["com.oculus.software.body_tracking"]),
        new(
            "Hand tracking",
            ["com.oculus.permission.HAND_TRACKING"],
            ["oculus.software.handtracking"],
            new Dictionary<string, string>
            {
                ["com.oculus.handtracking.frequency"] = "MAX",
                ["com.oculus.handtracking.version"] = "V2.0",
            }),
        new("Bluetooth", ["android.permission.BLUETOOTH", "android.permission.BLUETOOTH_CONNECT"], []),
        new("MRC workaround", [], [], null, ["libOVRMrcLib.oculus.so"]),
    ];
}
