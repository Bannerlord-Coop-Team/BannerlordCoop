using GameInterface.Services.GuantletMapEventVisuals;
using Xunit;

namespace GameInterface.Tests.Services.GuantletMapEventVisuals
{
    /// <summary>
    /// Tests the field-battle battle-size buckets used to re-apply a client map event's ambient sound
    /// size once its sides/parties have synced (see <see cref="MapEventBattleSizeCorrection"/>). The
    /// buckets must match vanilla GauntletMapEventVisual.GetBattleSizeValue.
    /// </summary>
    public class MapEventBattleSizeCorrectionTests
    {
        [Theory]
        [InlineData(0, 0)]
        [InlineData(29, 0)]
        [InlineData(30, 1)]
        [InlineData(79, 1)]
        [InlineData(80, 2)]
        [InlineData(119, 2)]
        [InlineData(120, 3)]
        [InlineData(500, 3)]
        public void ComputeBattleSize_BucketsByHeadcount(int numberOfInvolvedMen, int expected)
        {
            Assert.Equal(expected, MapEventBattleSizeCorrection.ComputeBattleSize(numberOfInvolvedMen));
        }
    }
}
