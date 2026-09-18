using System.Security.Cryptography;

namespace ModsBeforeFriday.Backend.Agent;

internal sealed class AgentBinaryProvider
{
    private readonly string _path;
    private AgentBinary? _cached;

    public AgentBinaryProvider(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = Path.GetFullPath(path);
    }

    public async Task<AgentBinary> GetAsync(CancellationToken cancellationToken)
    {
        if (_cached is not null)
        {
            return _cached;
        }

        if (!File.Exists(_path))
        {
            throw new FileNotFoundException(
                "The MBF Rust agent is not staged. Run eng/Fetch-MbfAgent.ps1 before building/running the app.",
                _path);
        }

        await using var stream = File.OpenRead(_path);
#pragma warning disable CA5350 // Do Not Use Weak Cryptographic Algorithms
        var hash = await SHA1.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
#pragma warning restore CA5350 // Do Not Use Weak Cryptographic Algorithms
        var sha1 = Convert.ToHexString(hash);
        _cached = new AgentBinary(_path, sha1);
        return _cached;
    }
}
