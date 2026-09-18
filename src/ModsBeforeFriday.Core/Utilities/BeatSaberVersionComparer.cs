namespace ModsBeforeFriday.Core.Utilities;

/// <summary>
/// Reproduces mbf-site's Beat Saber version ordering, including ignoring the
/// underscore build suffix used by Meta packages. Newer versions sort first.
/// </summary>
public sealed class BeatSaberVersionComparer : IComparer<string>
{
    public static BeatSaberVersionComparer Descending { get; } = new();

    public int Compare(string? x, string? y)
    {
        if (ReferenceEquals(x, y))
        {
            return 0;
        }

        if (x is null)
        {
            return 1;
        }

        if (y is null)
        {
            return -1;
        }

        var xSegments = ParseSegments(x);
        var ySegments = ParseSegments(y);
        var count = Math.Max(xSegments.Length, ySegments.Length);

        for (var i = 0; i < count; i++)
        {
            var a = i < xSegments.Length ? xSegments[i] : 0;
            var b = i < ySegments.Length ? ySegments[i] : 0;
            if (a > b)
            {
                return -1;
            }

            if (a < b)
            {
                return 1;
            }
        }

        return 0;
    }

    public static string TrimBuildSuffix(string version) => version.Split('_', 2)[0];

    private static int[] ParseSegments(string value) =>
        TrimBuildSuffix(value)
            .Split('.', StringSplitOptions.RemoveEmptyEntries)
            .Select(segment => int.TryParse(segment, out var parsed) ? parsed : 0)
            .ToArray();
}
