using System.Security.Cryptography;
using Microsoft.Extensions.Logging.Abstractions;
using ModsBeforeFriday.Backend.Agent;
using ModsBeforeFriday.Core.Agent;
using ModsBeforeFriday.Core.Models;
using Xunit;

namespace ModsBeforeFriday.Backend.Tests;

public sealed class MbfAgentClientTests
{
    [Fact]
    public async Task SendAsyncReportsLogsAndReturnsFinalResponse()
    {
        var temp = Path.GetTempFileName();
        try
        {
            await File.WriteAllBytesAsync(temp, "same-agent"u8.ToArray(), TestContext.Current.CancellationToken);
#pragma warning disable CA5350 // Do Not Use Weak Cryptographic Algorithms
            var sha = Convert.ToHexString(SHA1.HashData("same-agent"u8.ToArray()));
#pragma warning restore CA5350 // Do Not Use Weak Cryptographic Algorithms
            var adb = new FakeAdbClient
            {
                ShellOutput = sha,
                SessionResponse =
                    "{\"type\":\"LogMsg\",\"message\":\"Checking\",\"level\":\"Info\"}\n" +
                    "{\"type\":\"Mods\",\"installed_mods\":[]}\n",
            };
            var logs = new List<AgentLogEntry>();
            var progress = new InlineProgress<AgentLogEntry>(logs.Add);
            var client = new MbfAgentClient(
                adb,
                new AgentBinaryProvider(temp),
                new AgentRequestParameters("com.beatgames.beatsaber", false),
                NullLogger<MbfAgentClient>.Instance);

            var response = await client.SendAsync<ModsAgentResponse>(
                "serial",
                new QuickFixRequest
                {
                    AgentParameters = default!,
                    WipeExistingMods = false,
                },
                progress,
                CancellationToken.None);

            Assert.Empty(response.InstalledMods);
            Assert.Contains("\"type\":\"QuickFix\"", adb.CapturedSessionInput);
            Assert.Contains("\"agent_parameters\"", adb.CapturedSessionInput);
            Assert.Single(logs);
            Assert.Equal("Checking", logs[0].Message);
        }
        finally
        {
            File.Delete(temp);
        }
    }

    [Fact]
    public async Task SendAsyncThrowsLastAgentErrorWhenNoFinalResponseExists()
    {
        var temp = Path.GetTempFileName();
        try
        {
            await File.WriteAllBytesAsync(temp, "same-agent"u8.ToArray(), TestContext.Current.CancellationToken);
#pragma warning disable CA5350 // Do Not Use Weak Cryptographic Algorithms
            var sha = Convert.ToHexString(SHA1.HashData("same-agent"u8.ToArray()));
#pragma warning restore CA5350 // Do Not Use Weak Cryptographic Algorithms
            var adb = new FakeAdbClient
            {
                ShellOutput = sha,
                SessionResponse = "{\"type\":\"LogMsg\",\"message\":\"boom\",\"level\":\"Error\"}\n",
            };
            var client = new MbfAgentClient(
                adb,
                new AgentBinaryProvider(temp),
                new AgentRequestParameters("com.beatgames.beatsaber", false),
                NullLogger<MbfAgentClient>.Instance);

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => client.SendAsync<ModsAgentResponse>(
                "serial",
                new QuickFixRequest { AgentParameters = default!, WipeExistingMods = false },
                progress: null,
                CancellationToken.None));

            Assert.Equal("boom", exception.Message);
        }
        finally
        {
            File.Delete(temp);
        }
    }
    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}
