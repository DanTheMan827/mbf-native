using System.Text.Json;
using Microsoft.Extensions.Logging;
using ModsBeforeFriday.Backend.Configuration;
using ModsBeforeFriday.Core.Abstractions;
using ModsBeforeFriday.Core.Models;
using ModsBeforeFriday.Core.Utilities;

namespace ModsBeforeFriday.Backend.Mods;

internal sealed class ModCatalogService : IModCatalogService
{
    private static readonly JsonSerializerOptions RepositoryJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _httpClient;
    private readonly Uri _baseUri;
    private readonly ILogger<ModCatalogService> _logger;

    public ModCatalogService(HttpClient httpClient, BackendOptions options, ILogger<ModCatalogService> logger)
    {
        _httpClient = httpClient;
        _baseUri = options.ModRepositoryBaseUri;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ModCatalogEntry>> GetAvailableModsAsync(
        string gameVersion,
        IReadOnlyList<ModInfo> installedMods,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameVersion);
        ArgumentNullException.ThrowIfNull(installedMods);

        var gameTask = LoadRepositoryAsync(gameVersion, isGlobal: false, cancellationToken);
        var globalTask = LoadRepositoryAsync("global", isGlobal: true, cancellationToken);
        await Task.WhenAll(gameTask, globalTask).ConfigureAwait(false);

        var combined = new Dictionary<string, Dictionary<string, ModCatalogMod>>(globalTask.Result, StringComparer.Ordinal);
        foreach (var pair in gameTask.Result)
        {
            combined[pair.Key] = pair.Value;
        }

        var installed = installedMods.ToDictionary(mod => mod.Id, StringComparer.Ordinal);
        var display = new List<ModCatalogEntry>();
        foreach (var versions in combined.Values)
        {
            var latest = LatestVersion(versions.Values);
            if (latest is null)
            {
                continue;
            }

            var alreadyInstalled = installed.TryGetValue(latest.Id, out var existing);
            var needsUpdate = alreadyInstalled
                && existing is not null
                && !existing.IsCore
                && CompareVersions(latest.Version, existing.Version) > 0;

            if (needsUpdate || (!alreadyInstalled && !latest.IsGlobal))
            {
                display.Add(new ModCatalogEntry(latest, alreadyInstalled, needsUpdate));
            }
        }

        return display
            .OrderByDescending(item => item.NeedsUpdate)
            .ThenBy(item => item.Mod.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private async Task<Dictionary<string, Dictionary<string, ModCatalogMod>>> LoadRepositoryAsync(
        string version,
        bool isGlobal,
        CancellationToken cancellationToken)
    {
        var uri = new Uri(_baseUri, $"{Uri.EscapeDataString(version)}.json");
        _logger.LogDebug("Loading mod repository {Uri}", uri);
        using var response = await _httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

        var raw = await JsonSerializer.DeserializeAsync<Dictionary<string, Dictionary<string, ModCatalogDto>>>(
            stream,
            RepositoryJsonOptions,
            cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException($"Mod repository {uri} returned null JSON.");

        return raw.ToDictionary(
            id => id.Key,
            id => id.Value.ToDictionary(
                versionPair => versionPair.Key,
                versionPair => versionPair.Value.ToDomain(isGlobal),
                StringComparer.Ordinal),
            StringComparer.Ordinal);
    }

    private static ModCatalogMod? LatestVersion(IEnumerable<ModCatalogMod> mods)
    {
        ModCatalogMod? latest = null;
        foreach (var mod in mods)
        {
            if (latest is null || CompareVersions(mod.Version, latest.Version) > 0)
            {
                latest = mod;
            }
        }

        return latest;
    }

    private static int CompareVersions(string left, string right)
    {
        if (SemanticVersion.TryParse(left, out var leftVersion) && SemanticVersion.TryParse(right, out var rightVersion))
        {
            return leftVersion.CompareTo(rightVersion);
        }

        return StringComparer.OrdinalIgnoreCase.Compare(left, right);
    }

    private sealed record ModCatalogDto(
        string Name,
        string Id,
        string Version,
        string Download,
        string Source,
        string Author,
        string? Cover,
        string Modloader,
        string Description)
    {
        public ModCatalogMod ToDomain(bool isGlobal) => new(
            Id,
            Name,
            Version,
            Download,
            Source,
            Author,
            Cover,
            Modloader,
            Description,
            isGlobal);
    }
}
