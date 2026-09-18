using ModsBeforeFriday.Core.Utilities;
using Xunit;

namespace ModsBeforeFriday.Core.Tests;

public sealed class VersionTests
{
    [Fact]
    public void BeatSaberComparerSortsNewestFirstAndIgnoresBuildSuffix()
    {
        string[] versions = ["1.37.0_12345", "1.40.0", "1.39.1", "1.37"];

        Array.Sort(versions, BeatSaberVersionComparer.Descending);

        Assert.Equal(["1.40.0", "1.39.1", "1.37.0_12345", "1.37"], versions);
        Assert.Equal(0, BeatSaberVersionComparer.Descending.Compare("1.37", "1.37.0_999"));
    }

    [Theory]
    [InlineData("2.0.0", "1.9.9")]
    [InlineData("1.2.3", "1.2.3-beta.9")]
    [InlineData("1.2.3-beta.11", "1.2.3-beta.2")]
    [InlineData("1.2.3-rc.1", "1.2.3-beta.9")]
    public void SemanticVersionImplementsSemVerPrecedence(string newer, string older)
    {
        Assert.True(SemanticVersion.Parse(newer).CompareTo(SemanticVersion.Parse(older)) > 0);
    }
}
