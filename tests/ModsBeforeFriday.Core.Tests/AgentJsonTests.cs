using ModsBeforeFriday.Core.Agent;
using Xunit;

namespace ModsBeforeFriday.Core.Tests;

public sealed class AgentJsonTests
{
    [Fact]
    public void RequestSerializationMatchesRustTaggedShape()
    {
        AgentRequest request = new PatchRequest
        {
            AgentParameters = new AgentRequestParameters("com.beatgames.beatsaber", false),
            DowngradeTo = "1.39.1",
            ManifestMod = "<manifest />",
            Remodding = false,
            AllowNoCoreMods = false,
            DevicePreV51 = false,
            OverrideCoreModUrl = null,
        };

        var json = AgentJson.Serialize(request);

        Assert.Contains("\"type\":\"Patch\"", json);
        Assert.Contains("\"agent_parameters\"", json);
        Assert.Contains("\"manifest_mod\":\"<manifest />\"", json);
        Assert.Contains("\"downgrade_to\":\"1.39.1\"", json);
    }

    [Fact]
    public void ResponseDeserializationReadsTaggedLogMessage()
    {
        var response = AgentJson.DeserializeResponse("{\"type\":\"LogMsg\",\"message\":\"Working\",\"level\":\"Info\"}");

        var log = Assert.IsType<LogMessageAgentResponse>(response);
        Assert.Equal("Working", log.Message);
        Assert.Equal(AgentLogLevelDto.Info, log.Level);
    }
}
