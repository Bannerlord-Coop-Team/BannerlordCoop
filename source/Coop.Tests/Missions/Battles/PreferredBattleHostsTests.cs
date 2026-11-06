using GameInterface.Configuration;
using Missions.Battles;
using System;
using System.Collections.Generic;
using Xunit;

namespace Coop.Tests.Missions.Battles;

public class PreferredBattleHostsTests : IDisposable
{
    private readonly ModOptions previousOptions = ModConfigProvider.ModOptions;

    public void Dispose()
    {
        ModConfigProvider.ModOptions = previousOptions;
    }

    [Fact]
    public void Unset_RanksEveryoneUnlisted()
    {
        var hosts = Load(null);

        Assert.Equal(PreferredBattleHosts.Unlisted, hosts.RankOf("76561198000000001"));
    }

    [Fact]
    public void NullList_RanksEveryoneUnlisted()
    {
        ModConfigProvider.ModOptions = default;

        Assert.Null(ModConfigProvider.ModOptions.PreferredBattleHosts);
        Assert.Equal(PreferredBattleHosts.Unlisted, new PreferredBattleHosts().RankOf("76561198000000001"));
    }

    [Fact]
    public void BlankEntriesOnly_RankEveryoneUnlisted()
    {
        var hosts = Load(new List<string> { "", "  " });

        Assert.Equal(PreferredBattleHosts.Unlisted, hosts.RankOf("76561198000000001"));
        Assert.Equal(PreferredBattleHosts.Unlisted, hosts.RankOf(""));
    }

    [Fact]
    public void ListedPlayers_RankInListOrder()
    {
        var hosts = Load(new List<string> { "76561198000000002", " 76561198000000001 ", "Ctrl-A" });

        Assert.Equal(0, hosts.RankOf("76561198000000002"));
        Assert.Equal(1, hosts.RankOf("76561198000000001"));
        Assert.Equal(2, hosts.RankOf("ctrl-a"));
        Assert.Equal(PreferredBattleHosts.Unlisted, hosts.RankOf("76561198000000003"));
        Assert.Equal(PreferredBattleHosts.Unlisted, hosts.RankOf(null));
    }

    private static PreferredBattleHosts Load(List<string> preferredBattleHosts)
    {
        ModConfigProvider.LoadModConfig(new ModOptionsData { PreferredBattleHosts = preferredBattleHosts });
        return new PreferredBattleHosts();
    }
}
