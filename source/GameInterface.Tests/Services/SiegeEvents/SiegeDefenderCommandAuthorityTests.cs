using Common;
using Common.Util;
using GameInterface.Services.Players;
using GameInterface.Services.SiegeEvents;
using GameInterface.Services.SiegeEvents.Patches;
using HarmonyLib;
using Moq;
using SandBox.ViewModelCollection.MapSiege;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using TaleWorlds.Library;
using Xunit;

namespace GameInterface.Tests.Services.SiegeEvents;

[Collection(nameof(CampaignCurrentCollection))]
public class SiegeDefenderCommandAuthorityTests
{
    [Fact]
    public void HasPlayerDefender_NullSiege_ReturnsFalse()
    {
        var authority = new SiegeDefenderCommandAuthority(Mock.Of<IPlayerManager>());

        Assert.False(authority.HasPlayerDefender(null, BattleSideEnum.Defender));
    }

    [Fact]
    public void HasPlayerDefender_AttackerSide_ReturnsFalse()
    {
        var playerHero = ObjectHelper.SkipConstructor<Hero>();
        var settlement = CreateSettlement(CreateParty(playerHero));
        var siege = CreateSiege(settlement);
        var playerManager = new Mock<IPlayerManager>();
        playerManager.Setup(m => m.Contains(playerHero)).Returns(true);
        var authority = new SiegeDefenderCommandAuthority(playerManager.Object);

        Assert.False(authority.HasPlayerDefender(siege, BattleSideEnum.Attacker));
    }

    [Fact]
    public void HasPlayerDefender_DefenderNoParties_ReturnsFalse()
    {
        var settlement = CreateSettlement();
        var siege = CreateSiege(settlement);
        var authority = new SiegeDefenderCommandAuthority(Mock.Of<IPlayerManager>());

        Assert.False(authority.HasPlayerDefender(siege, BattleSideEnum.Defender));
    }

    [Fact]
    public void HasPlayerDefender_DefenderWithPlayerParty_ReturnsTrue()
    {
        var playerHero = ObjectHelper.SkipConstructor<Hero>();
        var settlement = CreateSettlement(CreateParty(playerHero));
        var siege = CreateSiege(settlement);
        var playerManager = new Mock<IPlayerManager>();
        playerManager.Setup(m => m.Contains(playerHero)).Returns(true);
        var authority = new SiegeDefenderCommandAuthority(playerManager.Object);

        Assert.True(authority.HasPlayerDefender(siege, BattleSideEnum.Defender));
    }

    [Fact]
    public void HasPlayerDefender_DefenderWithNonPlayerParties_ReturnsFalse()
    {
        var settlement = CreateSettlement(CreateParty(ObjectHelper.SkipConstructor<Hero>()));
        var siege = CreateSiege(settlement);
        var authority = new SiegeDefenderCommandAuthority(Mock.Of<IPlayerManager>());

        Assert.False(authority.HasPlayerDefender(siege, BattleSideEnum.Defender));
    }

    [Fact]
    public void HasPlayerDefender_DefenderPlayerPartyWithoutHero_ReturnsTrue()
    {
        var party = ObjectHelper.SkipConstructor<MobileParty>();
        var settlement = CreateSettlement(party);
        var siege = CreateSiege(settlement);
        var playerManager = new Mock<IPlayerManager>();
        playerManager.Setup(m => m.Contains(party)).Returns(true);
        var authority = new SiegeDefenderCommandAuthority(playerManager.Object);

        Assert.True(authority.HasPlayerDefender(siege, BattleSideEnum.Defender));
    }

    [Fact]
    public void Transpiler_ReplacesMainHeroComparisonWithCommandCheck()
    {
        MethodInfo method = AccessTools.Method(
            typeof(SandBox.View.Map.Managers.SettlementVisualManager),
            "TickSiegeMachineCircles");
        List<CodeInstruction> original = PatchProcessor.GetOriginalInstructions(method).ToList();
        MethodInfo mainHeroGetter = AccessTools.PropertyGetter(typeof(Hero), nameof(Hero.MainHero));

        Assert.Contains(original, instruction => instruction.Calls(mainHeroGetter));

        List<CodeInstruction> patched = SettlementSiegeDefenseCommandPatch.Transpiler(original).ToList();
        MethodInfo commandCheck = AccessTools.Method(
            typeof(SettlementSiegeDefenseCommandPatch),
            "IsCommandCapable",
            new[] { typeof(Hero) });

        Assert.DoesNotContain(patched, instruction => instruction.Calls(mainHeroGetter));
        Assert.Contains(patched, instruction => instruction.Calls(commandCheck));
    }

    [Fact]
    public void MapSiegeLeaderPostfix_OnServer_LeavesResultUnchanged()
    {
        bool previous = ModInformation.IsServer;
        ModInformation.IsServer = true;
        try
        {
            bool result = false;

            MapSiegeCommandAuthorityPatch.Postfix(ref result);

            Assert.False(result);
        }
        finally
        {
            ModInformation.IsServer = previous;
        }
    }

    [Fact]
    public void IsLocalDefenderJoined_DefendingBesiegedSettlement_ReturnsTrue()
    {
        var settlement = CreateSettlement();
        var siege = CreateSiege(settlement);
        var party = CreateMobileParty(AiBehavior.DefendSettlement, settlement);

        Assert.True(MapSiegeCommandAuthorityPatch.IsLocalDefenderJoined(party, siege));
    }

    [Theory]
    [InlineData(AiBehavior.Hold)]
    [InlineData(AiBehavior.GoToSettlement)]
    [InlineData(AiBehavior.BesiegeSettlement)]
    public void IsLocalDefenderJoined_OtherBehavior_ReturnsFalse(AiBehavior behavior)
    {
        var settlement = CreateSettlement();
        var siege = CreateSiege(settlement);
        var party = CreateMobileParty(behavior, settlement);

        Assert.False(MapSiegeCommandAuthorityPatch.IsLocalDefenderJoined(party, siege));
    }

    [Fact]
    public void IsLocalDefenderJoined_DefendingOtherSettlement_ReturnsFalse()
    {
        var siege = CreateSiege(CreateSettlement());
        var party = CreateMobileParty(AiBehavior.DefendSettlement, CreateSettlement());

        Assert.False(MapSiegeCommandAuthorityPatch.IsLocalDefenderJoined(party, siege));
    }

    [Fact]
    public void IsLocalDefenderJoined_NullParty_ReturnsFalse()
    {
        var siege = CreateSiege(CreateSettlement());

        Assert.False(MapSiegeCommandAuthorityPatch.IsLocalDefenderJoined(null, siege));
    }

    [Fact]
    public void IsLocalDefenderJoined_InsideWithoutDefendOrder_ReturnsFalse()
    {
        // Presence grants vanilla PlayerSiegeEvent/Side for free; only the join order counts.
        var settlement = CreateSettlement();
        var siege = CreateSiege(settlement);
        var party = CreateMobileParty(AiBehavior.Hold, settlement);
        AccessTools.Field(typeof(MobileParty), "_currentSettlement").SetValue(party, settlement);

        Assert.False(MapSiegeCommandAuthorityPatch.IsLocalDefenderJoined(party, siege));
    }

    [Fact]
    public void IsLocalDefenderJoined_DefendingFromOutside_ReturnsTrue()
    {
        var settlement = CreateSettlement();
        var siege = CreateSiege(settlement);
        var party = CreateMobileParty(AiBehavior.DefendSettlement, settlement);
        AccessTools.Field(typeof(MobileParty), "_currentSettlement").SetValue(party, CreateSettlement());

        Assert.True(MapSiegeCommandAuthorityPatch.IsLocalDefenderJoined(party, siege));
    }

    [Fact]
    public void IsLocalDefenderJoined_NullSiege_ReturnsFalse()
    {
        var party = CreateMobileParty(AiBehavior.DefendSettlement, CreateSettlement());

        Assert.False(MapSiegeCommandAuthorityPatch.IsLocalDefenderJoined(party, null));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void MapSiegeLeaderPostfix_WithoutCampaign_LeavesResultUnchanged(bool initial)
    {
        bool previousServer = ModInformation.IsServer;
        var previousCampaign = Campaign.Current;
        ModInformation.IsServer = false;
        Campaign.Current = null;
        try
        {
            bool result = initial;

            MapSiegeCommandAuthorityPatch.Postfix(ref result);

            Assert.Equal(initial, result);
        }
        finally
        {
            ModInformation.IsServer = previousServer;
            Campaign.Current = previousCampaign;
        }
    }

    [Fact]
    public void MapSiegeLeaderPatch_TargetsIsPlayerLeaderGetter()
    {
        MethodInfo getter = AccessTools.PropertyGetter(typeof(MapSiegeVM), "IsPlayerLeaderOfSiegeEvent");

        Assert.NotNull(getter);
    }

    private static MobileParty CreateParty(Hero leader)
    {
        var party = ObjectHelper.SkipConstructor<MobileParty>();
        var component = ObjectHelper.SkipConstructor<LordPartyComponent>();
        AccessTools.Field(typeof(LordPartyComponent), "_leader").SetValue(component, leader);
        AccessTools.Field(typeof(MobileParty), "_partyComponent").SetValue(party, component);
        return party;
    }

    private static MobileParty CreateMobileParty(AiBehavior behavior, Settlement targetSettlement)
    {
        var party = ObjectHelper.SkipConstructor<MobileParty>();
        AccessTools.Field(typeof(MobileParty), "_defaultBehavior").SetValue(party, behavior);
        AccessTools.Field(typeof(MobileParty), "_targetSettlement").SetValue(party, targetSettlement);
        return party;
    }

    private static Settlement CreateSettlement(params MobileParty[] parties)
    {
        var settlement = ObjectHelper.SkipConstructor<Settlement>();
        settlement._partiesCache = new MBList<MobileParty>();
        foreach (var party in parties)
            settlement._partiesCache.Add(party);
        return settlement;
    }

    private static SiegeEvent CreateSiege(Settlement settlement)
    {
        var siege = ObjectHelper.SkipConstructor<SiegeEvent>();
        AccessTools.Field(typeof(SiegeEvent), nameof(SiegeEvent.BesiegedSettlement)).SetValue(siege, settlement);
        return siege;
    }
}
