using System.Text;
using ModsBeforeFriday.Adb.Models;
using ModsBeforeFriday.Adb.Protocol;
using Xunit;

namespace ModsBeforeFriday.Adb.Tests;

public sealed class AdbSmartSocketClientTests
{
    [Fact]
    public async Task GetDevicesUsesLengthPrefixedHostServiceAndParsesMetadata()
    {
        var row = "ABC123\tdevice product:hollywood model:Quest_3 device:eureka transport_id:7\n";
#pragma warning disable CA1305 // Specify IFormatProvider
        var response = Encoding.ASCII.GetBytes("OKAY" + row.Length.ToString("X4") + row);
#pragma warning restore CA1305 // Specify IFormatProvider
        var transport = new ScriptedTransport(response);
        var client = new AdbSmartSocketClient(transport);

        var devices = await client.GetDevicesAsync(TestContext.Current.CancellationToken);

        var device = Assert.Single(devices);
        Assert.Equal("ABC123", device.Serial);
        Assert.Equal(AdbDeviceState.Device, device.State);
        Assert.Equal("Quest 3", device.Model);
        Assert.Equal(7, device.TransportId);

        var request = Encoding.ASCII.GetString(transport.OpenedStreams[0].WrittenBytes);
        Assert.Equal("000Ehost:devices-l", request);
    }


    [Fact]
    public async Task GetDevicesAcceptsWhitespaceSeparatedDeviceRows()
    {
        var row = "emulator-5554    device product:sdk_gpc_x86_64 model:sdk_gpc_x86_64 device:emu64xa transport_id:1\n";
#pragma warning disable CA1305 // Specify IFormatProvider
        var response = Encoding.ASCII.GetBytes("OKAY" + row.Length.ToString("X4") + row);
#pragma warning restore CA1305 // Specify IFormatProvider
        var transport = new ScriptedTransport(response);
        var client = new AdbSmartSocketClient(transport);

        var devices = await client.GetDevicesAsync(TestContext.Current.CancellationToken);

        var device = Assert.Single(devices);
        Assert.Equal("emulator-5554", device.Serial);
        Assert.Equal(AdbDeviceState.Device, device.State);
        Assert.Equal("sdk gpc x86 64", device.Model);
        Assert.Equal("sdk_gpc_x86_64", device.Product);
        Assert.Equal("emu64xa", device.Device);
        Assert.Equal(1, device.TransportId);
    }

    [Fact]
    public async Task GetDevicesParsesNoPermissionsState()
    {
        var row = "ABC123 no permissions transport_id:9\n";
#pragma warning disable CA1305 // Specify IFormatProvider
        var response = Encoding.ASCII.GetBytes("OKAY" + row.Length.ToString("X4") + row);
#pragma warning restore CA1305 // Specify IFormatProvider
        var transport = new ScriptedTransport(response);
        var client = new AdbSmartSocketClient(transport);

        var devices = await client.GetDevicesAsync(TestContext.Current.CancellationToken);

        var device = Assert.Single(devices);
        Assert.Equal("ABC123", device.Serial);
        Assert.Equal(AdbDeviceState.NoPermissions, device.State);
        Assert.Equal(9, device.TransportId);
    }

    [Fact]
    public async Task ShellSelectsTransportThenOpensClassicShell()
    {
        var response = Encoding.UTF8.GetBytes("OKAYOKAYAndroid 12\n");
        var transport = new ScriptedTransport(response);
        var client = new AdbSmartSocketClient(transport);

        var output = await client.ExecuteShellAsync("ABC123", "getprop ro.build.version.release", TestContext.Current.CancellationToken);

        Assert.Equal("Android 12\n", output);
        var request = Encoding.UTF8.GetString(transport.OpenedStreams[0].WrittenBytes);
        Assert.Contains("host:transport:ABC123", request);
        Assert.Contains("shell:getprop ro.build.version.release", request);
        Assert.Equal(0, transport.CompleteWritesCallCount);
    }

    [Fact]
    public async Task PushUsesSyncSendDataDoneProtocol()
    {
        var response = Encoding.ASCII.GetBytes("OKAYOKAYOKAY\0\0\0\0");
        var transport = new ScriptedTransport(response);
        var client = new AdbSmartSocketClient(transport);
        await using var source = new MemoryStream("agent"u8.ToArray());

        await client.PushAsync("ABC123", source, "/data/local/tmp/mbf-agent", cancellationToken: TestContext.Current.CancellationToken);

        var bytes = transport.OpenedStreams[0].WrittenBytes;
        var text = Encoding.ASCII.GetString(bytes);
        Assert.Contains("host:transport:ABC123", text);
        Assert.Contains("sync:", text);
        Assert.Contains("SEND", text);
        Assert.Contains("DATA", text);
        Assert.Contains("DONE", text);
    }
}
