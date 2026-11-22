using GameInterface.Utils;
using Xunit;

namespace GameInterface.Tests.Utils;

/// <summary>Covers the cross-process stable hash that machines use to derive identical RNG seeds.</summary>
public class StableHashTests
{
    /// <summary>Pinned FNV-1a values catch a swap to the per-process randomized string.GetHashCode.</summary>
    [Theory]
    [InlineData("", 2166136261u)]
    [InlineData("shared-map-event", 160666638u)]
    public void FromString_ReturnsPinnedFnv1aValue(string value, uint expected)
    {
        Assert.Equal(expected, StableHash.FromString(value));
    }
}
