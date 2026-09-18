using System.Text.Json;
using System.Text.Json.Serialization;

namespace ModsBeforeFriday.Core.Agent;

public static class AgentJson
{
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    public static string Serialize(AgentRequest request) => JsonSerializer.Serialize(request, Options);

    public static AgentResponse DeserializeResponse(string line) =>
        JsonSerializer.Deserialize<AgentResponse>(line, Options)
        ?? throw new JsonException("Agent response deserialized to null.");

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            PropertyNameCaseInsensitive = false,
            WriteIndented = false,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
