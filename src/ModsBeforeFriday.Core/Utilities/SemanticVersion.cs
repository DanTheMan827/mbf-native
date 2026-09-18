using System.Globalization;

namespace ModsBeforeFriday.Core.Utilities;

/// <summary>A small SemVer 2.0 precedence implementation used to avoid UI-layer package dependencies.</summary>
public readonly record struct SemanticVersion : IComparable<SemanticVersion>
{
    private readonly string[] _prerelease;

    public SemanticVersion(int major, int minor, int patch, string[]? prerelease = null)
    {
        Major = major;
        Minor = minor;
        Patch = patch;
        _prerelease = prerelease ?? [];
    }

    public int Major { get; }
    public int Minor { get; }
    public int Patch { get; }
    public IReadOnlyList<string> Prerelease => _prerelease;

    public static SemanticVersion Parse(string value)
    {
        if (!TryParse(value, out var result))
        {
            throw new FormatException($"'{value}' is not a valid semantic version.");
        }

        return result;
    }

    public static bool TryParse(string? value, out SemanticVersion result)
    {
        result = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var withoutBuild = value.Split('+', 2)[0];
        var split = withoutBuild.Split('-', 2);
        var core = split[0].Split('.');
        if (core.Length < 1 || core.Length > 3)
        {
            return false;
        }

        var numbers = new int[3];
        for (var i = 0; i < core.Length; i++)
        {
            if (!int.TryParse(core[i], NumberStyles.None, CultureInfo.InvariantCulture, out numbers[i]) || numbers[i] < 0)
            {
                return false;
            }
        }

        var prerelease = split.Length == 2
            ? split[1].Split('.', StringSplitOptions.RemoveEmptyEntries)
            : [];

        result = new SemanticVersion(numbers[0], numbers[1], numbers[2], prerelease);
        return true;
    }

    public int CompareTo(SemanticVersion other)
    {
        var core = Major.CompareTo(other.Major);
        if (core != 0) return core;
        core = Minor.CompareTo(other.Minor);
        if (core != 0) return core;
        core = Patch.CompareTo(other.Patch);
        if (core != 0) return core;

        if (_prerelease.Length == 0 && other._prerelease.Length == 0) return 0;
        if (_prerelease.Length == 0) return 1;
        if (other._prerelease.Length == 0) return -1;

        var count = Math.Max(_prerelease.Length, other._prerelease.Length);
        for (var i = 0; i < count; i++)
        {
            if (i >= _prerelease.Length) return -1;
            if (i >= other._prerelease.Length) return 1;

            var left = _prerelease[i];
            var right = other._prerelease[i];
            var leftNumeric = int.TryParse(left, NumberStyles.None, CultureInfo.InvariantCulture, out var leftNumber);
            var rightNumeric = int.TryParse(right, NumberStyles.None, CultureInfo.InvariantCulture, out var rightNumber);

            int part;
            if (leftNumeric && rightNumeric)
            {
                part = leftNumber.CompareTo(rightNumber);
            }
            else if (leftNumeric)
            {
                part = -1;
            }
            else if (rightNumeric)
            {
                part = 1;
            }
            else
            {
                part = string.CompareOrdinal(left, right);
            }

            if (part != 0) return part;
        }

        return 0;
    }

    public static bool operator <(SemanticVersion left, SemanticVersion right) => left.CompareTo(right) < 0;
    public static bool operator <=(SemanticVersion left, SemanticVersion right) => left.CompareTo(right) <= 0;
    public static bool operator >(SemanticVersion left, SemanticVersion right) => left.CompareTo(right) > 0;
    public static bool operator >=(SemanticVersion left, SemanticVersion right) => left.CompareTo(right) >= 0;
}
