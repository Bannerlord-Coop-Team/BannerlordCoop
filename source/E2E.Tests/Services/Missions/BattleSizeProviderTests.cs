using GameInterface.Configuration;
using Missions.Battles;
using Xunit;

namespace E2E.Tests.Services.Missions;

public class BattleSizeProviderTests
{
    [Fact]
    public void MissingSettingUsesDefaultBattleSize()
    {
        var provider = CreateProvider(null);

        Assert.Equal(1000, provider.GetBattleSize());
    }

    [Fact]
    public void ConfiguredBattleSizeIsUsed()
    {
        var provider = CreateProvider(750);

        Assert.Equal(750, provider.GetBattleSize());
    }

    [Theory]
    [InlineData(0, 200)]
    [InlineData(199, 200)]
    [InlineData(1001, 1000)]
    [InlineData(5000, 1000)]
    public void ConfiguredBattleSizeIsClamped(int configured, int expected)
    {
        var provider = CreateProvider(configured);

        Assert.Equal(expected, provider.GetBattleSize());
    }

    private static BattleSizeProvider CreateProvider(int? battleSize)
    {
        var config = new TestModConfig
        {
            Data = new ModConfigData
            {
                ModOptions = new ModOptionsData
                {
                    BattleSize = battleSize,
                },
            },
        };
        return new BattleSizeProvider(config);
    }

    private sealed class TestModConfig : IModConfig
    {
        public ModConfigData Data { get; set; } = new ModConfigData();
    }
}
