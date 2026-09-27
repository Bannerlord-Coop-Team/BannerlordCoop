using Common;
using Common.Util;
using GameInterface.Configuration;
using GameInterface.Services.Kingdoms;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using HarmonyLib;
using Moq;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Election;
using Xunit;
using CampaignKingdomDecision = TaleWorlds.CampaignSystem.Election.KingdomDecision;

namespace GameInterface.Tests.Services.Kingdoms;

[Collection(ModInformationRoleCollection.Name)]
public class OfflineWarProtectionTests : IDisposable
{
    private readonly bool wasServer = ModInformation.IsServer;
    private readonly ModOptions previousOptions = ModConfigProvider.ModOptions;
    private readonly Mock<IPlayerManager> playerManager = new();
    private readonly Mock<IObjectManager> objectManager = new();
    private readonly List<Player> players = new();
    private readonly HashSet<Player> connectedPlayers = new();
    private readonly HashSet<Clan> playerClans = new();
    private readonly OfflineWarProtection protection;

    private readonly Kingdom aiKingdom;
    private readonly Clan aiClan;
    private readonly Kingdom playerKingdom;
    private readonly Clan playerRulingClan;
    private readonly Player ruler;

    public OfflineWarProtectionTests()
    {
        ModInformation.IsServer = true;
        SetOption(true);

        playerManager.SetupGet(manager => manager.Players).Returns(players);
        playerManager.Setup(manager => manager.IsConnected(It.IsAny<Player>()))
            .Returns((Player player) => connectedPlayers.Contains(player));
        playerManager.Setup(manager => manager.Contains(It.IsAny<object>()))
            .Returns((object obj) => obj is Clan clan && playerClans.Contains(clan));
        protection = new OfflineWarProtection(playerManager.Object, objectManager.Object);

        aiKingdom = CreateKingdom(out aiClan);
        playerKingdom = CreateKingdom(out playerRulingClan);
        ruler = AddPlayer("ruler", playerRulingClan, isConnected: false);
    }

    public void Dispose()
    {
        ModInformation.IsServer = wasServer;
        ModConfigProvider.ModOptions = previousOptions;
    }

    [Theory]
    [InlineData(null)]
    [InlineData(false)]
    public void OptionAbsentOrOff_AllowsWarOnOfflinePlayerKingdom(bool? option)
    {
        ModConfigProvider.ModOptions = new ModOptions(new ModOptionsData { BlockAiWarDeclarationsOnOfflinePlayers = option });

        Assert.False(protection.ShouldRefuse(DeclareWar(aiClan, playerKingdom), out IFaction target));
        Assert.Null(target);
    }

    [Theory]
    [InlineData(nameof(DeclareWarDecision))]
    [InlineData(nameof(ProposeCallToWarAgreementDecision))]
    [InlineData(nameof(AcceptCallToWarAgreementDecision))]
    public void AiWarDecision_AgainstOfflinePlayerKingdom_IsRefused(string decisionType)
    {
        CampaignKingdomDecision decision = CreateWarDecision(decisionType, aiClan, playerKingdom);

        Assert.True(protection.ShouldRefuse(decision, out IFaction target));
        Assert.Same(playerKingdom, target);
    }

    [Fact]
    public void RulerOnline_AllowsWar()
    {
        connectedPlayers.Add(ruler);

        Assert.False(protection.ShouldRefuse(DeclareWar(aiClan, playerKingdom), out IFaction target));
        Assert.Null(target);
    }

    [Fact]
    public void PlayerVassalOfAiKingdom_CanProposeWarOnOfflinePlayerKingdom()
    {
        Clan vassalClan = CreateClan(aiKingdom);
        AddPlayer("vassal", vassalClan, isConnected: true);

        Assert.False(protection.ShouldRefuse(DeclareWar(vassalClan, playerKingdom), out _));
    }

    [Fact]
    public void AcceptCallToWar_ProposedByOnlinePlayerKingdom_IsAllowed()
    {
        // CoopKingdomElection authors this for the called player kingdom's ruling clan.
        CreateKingdom(out Clan calledRulingClan);
        AddPlayer("called", calledRulingClan, isConnected: true);
        var decision = AcceptCallToWar(calledRulingClan, aiKingdom, playerKingdom);

        Assert.False(protection.ShouldRefuse(decision, out _));
    }

    [Fact]
    public void AiKingdomTarget_IsAllowed()
    {
        Kingdom otherAiKingdom = CreateKingdom(out _);

        Assert.False(protection.ShouldRefuse(DeclareWar(aiClan, otherAiKingdom), out _));
    }

    [Fact]
    public void OfflinePlayerServingInAiKingdom_IsNotProtected()
    {
        Kingdom otherAiKingdom = CreateKingdom(out _);
        AddPlayer("servant", CreateClan(otherAiKingdom), isConnected: false);

        Assert.False(protection.ShouldRefuse(DeclareWar(aiClan, otherAiKingdom), out _));
    }

    [Fact]
    public void RulerOffline_PlayerVassalOnline_AllowsWar()
    {
        AddPlayer("vassal", CreateClan(playerKingdom), isConnected: true);

        Assert.False(protection.ShouldRefuse(DeclareWar(aiClan, playerKingdom), out _));
    }

    [Fact]
    public void RulerOffline_PlayerMercenaryOnline_KeepsProtection()
    {
        // A mercenary has no vote on the kingdom's war or peace.
        Clan mercenaryClan = CreateClan(playerKingdom);
        mercenaryClan.IsUnderMercenaryService = true;
        AddPlayer("mercenary", mercenaryClan, isConnected: true);

        Assert.True(protection.ShouldRefuse(DeclareWar(aiClan, playerKingdom), out _));
    }

    [Fact]
    public void OnlinePlayerOfAnotherFaction_DoesNotProtectTarget()
    {
        AddPlayer("elsewhere", CreateClan(aiKingdom), isConnected: true);

        Assert.True(protection.ShouldRefuse(DeclareWar(aiClan, playerKingdom), out _));
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void IndependentPlayerClan_IsProtectedOnlyWhileOffline(bool isConnected, bool expectedRefused)
    {
        Clan independentClan = CreateClan();
        AddPlayer("independent", independentClan, isConnected);

        Assert.Equal(expectedRefused, protection.ShouldRefuse(DeclareWar(aiClan, independentClan), out IFaction target));
        Assert.Same(expectedRefused ? independentClan : null, target);
    }

    [Theory]
    [InlineData(false, false, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    public void CoopClan_IsProtectedOnlyWhileAllMembersAreOffline(bool rulerOnline, bool memberOnline, bool expectedRefused)
    {
        if (rulerOnline) connectedPlayers.Add(ruler);
        AddPlayer("member", playerRulingClan, memberOnline);

        Assert.Equal(expectedRefused, protection.ShouldRefuse(DeclareWar(aiClan, playerKingdom), out _));
    }

    [Fact]
    public void CoopClanMember_WithStaleRegisteredClan_CountsFromHeroClan()
    {
        // The member joined the ruling clan this session, so Player.ClanId still names the old clan.
        AddPlayer("member", playerRulingClan, isConnected: true, registeredClan: CreateClan());

        Assert.False(protection.ShouldRefuse(DeclareWar(aiClan, playerKingdom), out _));
    }

    [Fact]
    public void ConnectedPlayer_WithoutResolvableHero_CountsFromRegisteredClan()
    {
        var player = new Player("nohero", "missing_hero", "nohero_party", "nohero_clan", "nohero_character");
        Clan registeredClan = CreateClan(playerKingdom);
        objectManager.Setup(manager => manager.TryGetObject("nohero_clan", out registeredClan)).Returns(true);
        players.Add(player);
        connectedPlayers.Add(player);

        Assert.False(protection.ShouldRefuse(DeclareWar(aiClan, playerKingdom), out _));
    }

    [Fact]
    public void ConnectedPlayer_WithHeroOutsideAnyClan_CountsFromRegisteredClan()
    {
        AddPlayer("clanless", heroClan: null, isConnected: true, registeredClan: CreateClan(playerKingdom));

        Assert.False(protection.ShouldRefuse(DeclareWar(aiClan, playerKingdom), out _));
    }

    [Fact]
    public void PlayerClanInsideAKingdom_AsTarget_IsNotProtectedOnItsOwn()
    {
        // Only a kingdom or an independent clan is a war target the rule protects.
        Clan vassalClan = CreateClan(aiKingdom);
        AddPlayer("vassal", vassalClan, isConnected: false);

        Assert.False(protection.ShouldRefuse(DeclareWar(aiClan, vassalClan), out _));
    }

    [Theory]
    [InlineData(typeof(MakePeaceKingdomDecision), nameof(MakePeaceKingdomDecision.FactionToMakePeaceWith))]
    [InlineData(typeof(StartAllianceDecision), nameof(StartAllianceDecision.KingdomToStartAllianceWith))]
    [InlineData(typeof(TradeAgreementDecision), nameof(TradeAgreementDecision.TargetKingdom))]
    [InlineData(typeof(KingdomPolicyDecision), null)]
    public void NonWarDecisions_AgainstOfflinePlayerKingdom_AreAllowed(Type decisionType, string? targetField)
    {
        var decision = (CampaignKingdomDecision)ObjectHelper.SkipConstructor(decisionType);
        decision.ProposerClan = aiClan;
        if (targetField != null) AccessTools.Field(decisionType, targetField).SetValue(decision, playerKingdom);

        Assert.False(protection.ShouldRefuse(decision, out _));
    }

    [Fact]
    public void OddData_FailsOpenWithoutThrowing()
    {
        Kingdom noRulerKingdom = CreateKingdom(out _);
        noRulerKingdom._rulingClan = null;
        Kingdom eliminatedKingdom = CreateKingdom(out Clan eliminatedRulingClan);
        AddPlayer("eliminated", eliminatedRulingClan, isConnected: false);
        eliminatedKingdom._isEliminated = true;
        var noProposer = DeclareWar(null!, playerKingdom);

        Assert.False(protection.ShouldRefuse(null!, out _));
        Assert.False(protection.ShouldRefuse(noProposer, out _));
        Assert.False(protection.ShouldRefuse(DeclareWar(aiClan, null!), out _));
        Assert.False(protection.ShouldRefuse(DeclareWar(aiClan, noRulerKingdom), out _));
        Assert.False(protection.ShouldRefuse(DeclareWar(aiClan, eliminatedKingdom), out _));
    }

    [Fact]
    public void RunningAsClient_IsAllowed()
    {
        ModInformation.IsServer = false;

        Assert.False(protection.ShouldRefuse(DeclareWar(aiClan, playerKingdom), out _));
    }

    private static void SetOption(bool enabled)
    {
        ModConfigProvider.ModOptions = new ModOptions(new ModOptionsData { BlockAiWarDeclarationsOnOfflinePlayers = enabled });
    }

    private static Kingdom CreateKingdom(out Clan rulingClan)
    {
        var kingdom = ObjectHelper.SkipConstructor<Kingdom>();
        rulingClan = CreateClan(kingdom);
        kingdom._rulingClan = rulingClan;
        return kingdom;
    }

    private static Clan CreateClan(Kingdom? kingdom = null)
    {
        var clan = ObjectHelper.SkipConstructor<Clan>();
        clan._kingdom = kingdom;
        return clan;
    }

    private Player AddPlayer(string controllerId, Clan? heroClan, bool isConnected, Clan? registeredClan = null)
    {
        var hero = ObjectHelper.SkipConstructor<Hero>();
        hero._clan = heroClan;
        Clan storedClan = (registeredClan ?? heroClan)!;
        var player = new Player(controllerId, controllerId + "_hero", controllerId + "_party", controllerId + "_clan", controllerId + "_character");

        objectManager.Setup(manager => manager.TryGetObject(player.HeroId, out hero)).Returns(true);
        objectManager.Setup(manager => manager.TryGetObject(player.ClanId, out storedClan)).Returns(true);
        players.Add(player);
        playerClans.Add(heroClan ?? storedClan);
        if (isConnected) connectedPlayers.Add(player);
        return player;
    }

    private static CampaignKingdomDecision CreateWarDecision(string decisionType, Clan proposer, Kingdom target) => decisionType switch
    {
        nameof(DeclareWarDecision) => DeclareWar(proposer, target),
        nameof(ProposeCallToWarAgreementDecision) => ProposeCallToWar(proposer, target),
        nameof(AcceptCallToWarAgreementDecision) => AcceptCallToWar(proposer, null!, target),
        _ => throw new ArgumentOutOfRangeException(nameof(decisionType)),
    };

    private static DeclareWarDecision DeclareWar(Clan proposer, IFaction target)
    {
        var decision = ObjectHelper.SkipConstructor<DeclareWarDecision>();
        decision.ProposerClan = proposer;
        AccessTools.Field(typeof(DeclareWarDecision), nameof(DeclareWarDecision.FactionToDeclareWarOn)).SetValue(decision, target);
        return decision;
    }

    private static ProposeCallToWarAgreementDecision ProposeCallToWar(Clan proposer, Kingdom target)
    {
        var decision = ObjectHelper.SkipConstructor<ProposeCallToWarAgreementDecision>();
        decision.ProposerClan = proposer;
        AccessTools.Field(typeof(ProposeCallToWarAgreementDecision), nameof(ProposeCallToWarAgreementDecision.KingdomToCallToWarAgainst))
            .SetValue(decision, target);
        return decision;
    }

    private static AcceptCallToWarAgreementDecision AcceptCallToWar(Clan proposer, Kingdom callingKingdom, Kingdom target)
    {
        var decision = ObjectHelper.SkipConstructor<AcceptCallToWarAgreementDecision>();
        decision.ProposerClan = proposer;
        AccessTools.Field(typeof(AcceptCallToWarAgreementDecision), nameof(AcceptCallToWarAgreementDecision.CallingKingdom))
            .SetValue(decision, callingKingdom);
        AccessTools.Field(typeof(AcceptCallToWarAgreementDecision), nameof(AcceptCallToWarAgreementDecision.KingdomToCallToWarAgainst))
            .SetValue(decision, target);
        return decision;
    }
}
