using System.Text.Json;
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
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Equal("Patch", root.GetProperty("type").GetString());
        Assert.Equal("1.39.1", root.GetProperty("downgrade_to").GetString());
        Assert.Equal("<manifest />", root.GetProperty("manifest_mod").GetString());

        var parameters = root.GetProperty("agent_parameters");
        Assert.Equal("com.beatgames.beatsaber", parameters.GetProperty("game_id").GetString());
        Assert.False(parameters.GetProperty("ignore_package_id").GetBoolean());
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
