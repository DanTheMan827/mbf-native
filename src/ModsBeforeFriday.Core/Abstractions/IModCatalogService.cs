using ModsBeforeFriday.Core.Models;

namespace ModsBeforeFriday.Core.Abstractions;

public interface IModCatalogService
{
    Task<IReadOnlyList<ModCatalogEntry>> GetAvailableModsAsync(
        string gameVersion,
        IReadOnlyList<ModInfo> installedMods,
        CancellationToken cancellationToken = default);
}
