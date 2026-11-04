using GameInterface.Configuration;
using Missions.Battles;
using System.Collections.Generic;
using Xunit;

namespace Coop.Tests.Missions.Battles;

public class PreferredBattleHostsTests
{
    [Fact]
    public void Unset_IsEmptyAndRanksNobody()
    {
        var hosts = new PreferredBattleHosts(new TestModConfig(null));

        Assert.True(hosts.IsEmpty);
        Assert.Equal(-1, hosts.RankOf("76561198000000001"));
    }

    [Fact]
    public void BlankEntriesOnly_IsEmpty()
    {
        var hosts = new PreferredBattleHosts(new TestModConfig(new List<string> { "", "  " }));

        Assert.True(hosts.IsEmpty);
    }

    [Fact]
    public void ListedPlayers_RankInListOrder()
    {
        var hosts = new PreferredBattleHosts(new TestModConfig(
            new List<string> { "76561198000000002", " 76561198000000001 ", "Ctrl-A" }));

        Assert.False(hosts.IsEmpty);
        Assert.Equal(0, hosts.RankOf("76561198000000002"));
        Assert.Equal(1, hosts.RankOf("76561198000000001"));
        Assert.Equal(2, hosts.RankOf("ctrl-a"));
        Assert.Equal(-1, hosts.RankOf("76561198000000003"));
        Assert.Equal(-1, hosts.RankOf(null));
    }

    private sealed class TestModConfig : IModConfig
    {
        public TestModConfig(List<string> preferredBattleHosts)
        {
            Data.ModOptions.PreferredBattleHosts = preferredBattleHosts;
        }

        public ModConfigData Data { get; } = new ModConfigData();
    }
}
