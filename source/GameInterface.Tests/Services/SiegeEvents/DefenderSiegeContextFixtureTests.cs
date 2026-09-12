#if DEBUG
using Common;
using Common.Messaging;
using Common.Network;
using Common.Network.Messages;
using Common.Tests.Utils;
using Coop.Core.Server.Connections;
using GameInterface.Services.MapEvents.Messages;
using Common.Util;
using Coop.Tests.Mocks;
using GameInterface.Services.MapEvents.Messages.Leave;
using GameInterface.Services.MobileParties.Data;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Services.SiegeEvents.Interfaces;
using GameInterface.Services.Villages.Commands;
using GameInterface.Tests.Bootstrap;
using HarmonyLib;
using LiteNetLib;
using Moq;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using GameInterface.Services.SiegeEvents.Commands;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using Xunit;

namespace GameInterface.Tests.Services.SiegeEvents;

[Collection(nameof(CampaignCurrentCollection))]
public class DefenderSiegeContextFixtureTests
{
    private static bool failCreatedBesiegerCompanionRemoval;
    private static global::GameInterface.Services.ObjectManager.ObjectManager createdBesiegerObjects;
    private static MobileParty createdBesiegerCandidate;
    private static Hero createdBesiegerHero;
    private static Clan createdBesiegerClan;
    private static bool postCreationSnapshotAttempted;

    [Fact]
    public void FixtureClan_UsesLeaderlessHostileBanditWithoutChangingDiplomacy()
    {
        using var test = new AssaultFixture();
        var defender = new Clan();
        var bandits = new Clan();
        bandits.IsBanditFaction = true;
        bandits._defaultPartyTemplate = ObjectHelper.SkipConstructor<PartyTemplateObject>();
        var templateClan = new Clan();
        templateClan._defaultPartyTemplate = ObjectHelper.SkipConstructor<PartyTemplateObject>();
        var templateHero = ObjectHelper.SkipConstructor<Hero>();
        templateHero._characterObject = ObjectHelper.SkipConstructor<CharacterObject>();
        templateClan._leader = templateHero;
        var originalWar = bandits.MapFaction.IsAtWarWith(defender);
        Assert.True(originalWar);
        var selected = DefenderSiegeContextFixture.FindFixtureClan(
            new[] { templateClan, bandits }, defender, new[] { defender }, out var template);
        Assert.Same(bandits, selected);
        Assert.Same(templateHero.CharacterObject, template);
        Assert.Null(bandits.Leader);
        Assert.Equal(originalWar, bandits.MapFaction.IsAtWarWith(defender));
        Assert.Null(DefenderSiegeContextFixture.FindFixtureClan(
            new[] { templateClan, bandits }, defender, new[] { bandits }, out _));
        bandits._leader = templateHero;
        Assert.Same(bandits, DefenderSiegeContextFixture.FindFixtureClan(
            new[] { templateClan, bandits }, defender, new[] { defender }, out template));
        Assert.Same(templateHero.CharacterObject, template);
    }

    [Fact]
    public void RestoredBehavior_PreservesCapturedRoutingPositionAndNavigation()
    {
        var expected = new PartyBehaviorUpdateData("besieger", AiBehavior.Hold, "anchor", default,
            new CampaignVec2(new Vec2(12f, 24f), true), AiBehavior.Hold, default, default)
        {
            TargetPartyId = "target", MoveTargetPartyId = "moving-target", TargetSettlementId = "castle_ES1",
            IsInteractableAnchor = true, IsTargetingPort = true
        };
        Assert.True(DefenderSiegeContextFixture.SameBehavior(expected, expected));
        var changed = expected;
        changed.PartyPosition = new CampaignVec2(new Vec2(13f, 24f), true);
        Assert.False(DefenderSiegeContextFixture.SameBehavior(expected, changed));
        changed = expected;
        changed.MoveTargetPartyId = null;
        Assert.False(DefenderSiegeContextFixture.SameBehavior(expected, changed));
        changed = expected;
        changed.TargetSettlementId = "town_ES1";
        Assert.False(DefenderSiegeContextFixture.SameBehavior(expected, changed));
        changed = expected;
        changed.IsCurrentlyAtSea = true;
        Assert.False(DefenderSiegeContextFixture.SameBehavior(expected, changed));
        changed = expected;
        changed.IsInteractableAnchor = false;
        Assert.False(DefenderSiegeContextFixture.SameBehavior(expected, changed));
        changed = expected;
        changed.IsTargetingPort = false;
        Assert.False(DefenderSiegeContextFixture.SameBehavior(expected, changed));
    }

    [Fact]
    public void CreatedBesiegerCleanup_RequiresDestroyedPartyDetachedCompanionAndRemovedHero()
    {
        var removed = KillCharacterAction.KillCharacterActionDetail.Lost;
        Assert.True(DefenderSiegeContextFixture.IsCreatedBesiegerRestored(
            partyActive: false,
            companionAttached: false,
            deathMark: removed));
        Assert.False(DefenderSiegeContextFixture.IsCreatedBesiegerRestored(
            partyActive: true,
            companionAttached: false,
            deathMark: removed));
        Assert.False(DefenderSiegeContextFixture.IsCreatedBesiegerRestored(
            partyActive: false,
            companionAttached: true,
            deathMark: removed));
        Assert.False(DefenderSiegeContextFixture.IsCreatedBesiegerRestored(
            partyActive: false,
            companionAttached: false,
            deathMark: KillCharacterAction.KillCharacterActionDetail.None));
    }

    [Fact]
    public void CreatedBesiegerPostCreationException_RollsBackRetainedOwnership()
    {
        using var test = new AssaultFixture();
        test.PrepareCreatedBesiegerPostCreationException();

        var harmony = new Harmony("created-besieger-post-creation-" + Guid.NewGuid().ToString("N"));
        try
        {
            harmony.Patch(
                AccessTools.Method(typeof(DefenderSiegeContextFixture), "FindRestorableBesieger"),
                prefix: new HarmonyMethod(typeof(DefenderSiegeContextFixtureTests), nameof(FindNoRestorableBesiegerPrefix)));
            harmony.Patch(
                AccessTools.Method(typeof(DefenderSiegeContextFixture), "TryCreateFixtureBesieger"),
                prefix: new HarmonyMethod(typeof(DefenderSiegeContextFixtureTests), nameof(CreatePostCreationBesiegerPrefix)));
            harmony.Patch(
                AccessTools.PropertyGetter(typeof(Settlement), nameof(Settlement.IsCastle)),
                prefix: new HarmonyMethod(typeof(DefenderSiegeContextFixtureTests), nameof(IsCastlePrefix)));
            harmony.Patch(
                AccessTools.Method(typeof(DestroyPartyAction), nameof(DestroyPartyAction.Apply),
                    new[] { typeof(PartyBase), typeof(MobileParty) }),
                prefix: new HarmonyMethod(typeof(DefenderSiegeContextFixtureTests), nameof(DestroyCreatedBesiegerPartyPrefix)));
            harmony.Patch(
                AccessTools.Method(typeof(RemoveCompanionAction), nameof(RemoveCompanionAction.ApplyByFire)),
                prefix: new HarmonyMethod(typeof(DefenderSiegeContextFixtureTests), nameof(RemoveCreatedBesiegerCompanionPrefix)));
            harmony.Patch(
                AccessTools.Method(typeof(KillCharacterAction), nameof(KillCharacterAction.ApplyByRemove),
                    new[] { typeof(Hero), typeof(bool), typeof(bool) }),
                prefix: new HarmonyMethod(typeof(DefenderSiegeContextFixtureTests), nameof(RemoveCreatedBesiegerHeroPrefix)));
            createdBesiegerObjects = test.Objects;

            var result = test.Fixture.Capture();
            Assert.False(result.Succeeded);
            var evidence = JObject.Parse(result.Output.Substring("LIVE_TEST_JSON=".Length));
            Assert.Equal("created_besieger_capture_exception", (string)evidence["status"]);
            Assert.StartsWith("besieger_behavior_snapshot: System.InvalidOperationException:",
                (string)evidence["captureFailureDetail"]);
            Assert.True(postCreationSnapshotAttempted);
            Assert.False(test.HasCreatedBesiegerOwnership);
            Assert.Null(test.CapturedBesieger);
            Assert.False(test.Objects.TryGetObject<MobileParty>(createdBesiegerCandidate.StringId, out _));
            Assert.Null(createdBesiegerHero.CompanionOf);
            Assert.DoesNotContain(createdBesiegerHero, createdBesiegerClan.Companions);
        }
        finally
        {
            createdBesiegerObjects = null;
            createdBesiegerCandidate = null;
            createdBesiegerHero = null;
            createdBesiegerClan = null;
            postCreationSnapshotAttempted = false;
            harmony.UnpatchAll(harmony.Id);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CreatedBesiegerCleanupRetry_DoesNotRequireDestroyedPartyRegistration(bool ownsClanLeader)
    {
        using var test = new AssaultFixture();
        int companionBaseline = test.PrepareCreatedBesiegerCleanupRetry(ownsClanLeader);

        var harmony = new Harmony("created-besieger-cleanup-retry-" + Guid.NewGuid().ToString("N"));
        try
        {
            harmony.Patch(
                AccessTools.Method(typeof(DestroyPartyAction), nameof(DestroyPartyAction.Apply),
                    new[] { typeof(PartyBase), typeof(MobileParty) }),
                prefix: new HarmonyMethod(typeof(DefenderSiegeContextFixtureTests), nameof(DestroyCreatedBesiegerPartyPrefix)));
            harmony.Patch(
                AccessTools.Method(typeof(RemoveCompanionAction), nameof(RemoveCompanionAction.ApplyByFire)),
                prefix: new HarmonyMethod(typeof(DefenderSiegeContextFixtureTests), nameof(RemoveCreatedBesiegerCompanionPrefix)));
            harmony.Patch(
                AccessTools.Method(typeof(KillCharacterAction), nameof(KillCharacterAction.ApplyByRemove),
                    new[] { typeof(Hero), typeof(bool), typeof(bool) }),
                prefix: new HarmonyMethod(typeof(DefenderSiegeContextFixtureTests), nameof(RemoveCreatedBesiegerHeroPrefix)));
            createdBesiegerObjects = test.Objects;
            failCreatedBesiegerCompanionRemoval = true;

            Assert.False(test.Fixture.Restore().Succeeded);
            Assert.True(test.HasCreatedBesiegerOwnership);
            Assert.False(test.Objects.TryGetObject<MobileParty>("destroyed-created-besieger", out _));
            Assert.True(test.Fixture.Restore().Succeeded);
            Assert.True(test.Fixture.Verify().Succeeded);
            Assert.Equal(companionBaseline, test.CreatedBesiegerClan.Companions.Count());
            Assert.DoesNotContain(test.CreatedBesiegerHero, test.CreatedBesiegerClan.Companions);
            Assert.Null(test.CreatedBesiegerHero.CompanionOf);
            if (ownsClanLeader)
            {
                Assert.Null(test.CreatedBesiegerClan.Leader);
                Assert.Null(test.CreatedBesiegerHero.Clan);
                Assert.DoesNotContain(test.CreatedBesiegerHero, test.CreatedBesiegerClan.Heroes);
                Assert.True(test.Fixture.Restore().Succeeded);
            }
        }
        finally
        {
            failCreatedBesiegerCompanionRemoval = false;
            createdBesiegerObjects = null;
            harmony.UnpatchAll(harmony.Id);
        }
    }

    [Fact]
    public void CreatedBesiegerCleanup_RefusesAnotherLeaderWithoutDestroyingOwnedParty()
    {
        using var test = new AssaultFixture();
        test.PrepareCreatedBesiegerCleanupRetry(ownsClanLeader: true);
        var replacement = ObjectHelper.SkipConstructor<Hero>();
        test.CreatedBesiegerClan._leader = replacement;

        Assert.False(test.Fixture.Restore().Succeeded);
        Assert.True(test.HasCreatedBesiegerOwnership);
        Assert.True(test.Objects.TryGetObject<MobileParty>("destroyed-created-besieger", out var party));
        Assert.True(party.IsActive);
        Assert.Same(replacement, test.CreatedBesiegerClan.Leader);
        Assert.Same(test.CreatedBesiegerClan, test.CreatedBesiegerHero.CompanionOf);
    }

    private static bool DestroyCreatedBesiegerPartyPrefix(PartyBase __0, MobileParty __1)
    {
        Assert.NotNull(createdBesiegerObjects);
        Assert.True(createdBesiegerObjects.Remove(__1));
        __1.IsActive = false;
        return false;
    }

    private static bool RemoveCreatedBesiegerCompanionPrefix(Clan __0, Hero __1)
    {
        if (failCreatedBesiegerCompanionRemoval)
        {
            failCreatedBesiegerCompanionRemoval = false;
            throw new InvalidOperationException("fixture companion removal failed once");
        }
        __1._companionOf = null;
        __0._companionsCache.Remove(__1);
        return false;
    }

    private static bool RemoveCreatedBesiegerHeroPrefix(Hero __0)
    {
        __0.DeathMark = KillCharacterAction.KillCharacterActionDetail.Lost;
        return false;
    }

    private static bool FindNoRestorableBesiegerPrefix(ref MobileParty __result)
    {
        __result = null;
        return false;
    }

    private static bool CreatePostCreationBesiegerPrefix(
        ref MobileParty candidate,
        ref Hero fixtureHero,
        ref Clan fixtureClan,
        ref bool cleanupFailed,
        ref bool ownsClanLeader,
        ref string failureDetail,
        ref bool __result)
    {
        candidate = createdBesiegerCandidate;
        fixtureHero = createdBesiegerHero;
        fixtureClan = createdBesiegerClan;
        cleanupFailed = false;
        ownsClanLeader = false;
        failureDetail = null;
        __result = true;
        return false;
    }

    private static bool IsCastlePrefix(ref bool __result)
    {
        __result = true;
        return false;
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Cleanup_UnresolvedAssault_ExitsOnlyConnectedDefendersThenRestores(int disconnectedCount)
    {
        using var test = new AssaultFixture();
        foreach (string id in test.Players.Keys.Take(disconnectedCount)) test.Disconnected.Add(id);

        Assert.True(test.Fixture.EndMissions().Succeeded);
        Assert.Equal(2 - disconnectedCount, test.ExitRequests.Count);
        Assert.DoesNotContain(test.ExitRequests, peer => test.Disconnected.Contains(test.PeerOwners[peer]));
        Assert.True(test.Fixture.Restore().Succeeded);
        Assert.Equal(1, test.FinalizeRequests);
        Assert.Equal(1, test.BreakRequests);
        Assert.True(test.Fixture.Verify().Succeeded);
    }

    [Theory]
    [InlineData("hostile-lord", false)]
    [InlineData("inactive", true)]
    [InlineData("caravan", true)]
    [InlineData("villager", true)]
    [InlineData("non-hostile", true)]
    [InlineData("militia", false)]
    [InlineData("rebellious-militia", true)]
    public void StartMembership_OnlyRequiresCapturedVanillaAssaultDefenders(string visitorKind, bool allowed)
    {
        using var test = new AssaultFixture();
        var visitor = test.PrepareStartWithVisitor(visitorKind);
        Assert.True(test.Fixture.HasOnlyCapturedAssaultDefenders(test.Defenders));
        Assert.Equal(allowed, test.Fixture.HasOnlyCapturedAssaultDefenders(test.Defenders.Append(visitor)));
    }

    [Fact]
    public void Start_UncapturedEligibleDefender_RefusesBeforeStartingSiege()
    {
        using var test = new AssaultFixture();
        test.PrepareStartWithVisitor("hostile-lord");
        Assert.False(test.Fixture.Start().Succeeded);
        Assert.False(test.StartAttempted);
        test.Siege.Verify(value => value.StartSiegeEvent(It.IsAny<MobileParty>(), It.IsAny<Settlement>()), Times.Never);
        Assert.Null(test.Settlement.SiegeEvent);
    }

    [Fact]
    public void Cleanup_UncapturedAssaultMember_DoesNotFinalizeForeignParty()
    {
        using var test = new AssaultFixture();
        test.AddUncapturedDefender();
        Assert.False(test.Fixture.EndMissions().Succeeded);
        Assert.False(test.Fixture.Restore().Succeeded);
        Assert.Empty(test.ExitRequests);
        Assert.Equal(0, test.FinalizeRequests);
        Assert.Equal(0, test.BreakRequests);
    }

    [Theory]
    [InlineData("campaign")]
    [InlineData("party")]
    [InlineData("player")]
    [InlineData("settlement")]
    [InlineData("resolved-battle")]
    public void Cleanup_DisconnectedDefender_DoesNotBypassCapturedIdentityOrOutcome(string change)
    {
        using var test = new AssaultFixture();
        test.Disconnected.Add("testclient");
        switch (change)
        {
            case "campaign": Campaign.Current = ObjectHelper.SkipConstructor<Campaign>(); break;
            case "party":
                Assert.True(test.Objects.Remove(test.Defenders[0]));
                Assert.True(test.Objects.AddExisting("party_testclient", AssaultFixture.CreateParty("replacement")));
                break;
            case "player":
                test.Players["testclient"] = new Player("testclient", "hero", "party_testclient", "clan", "character");
                break;
            case "settlement":
                Assert.True(test.Objects.Remove(test.Settlement));
                Assert.True(test.Objects.AddExisting("castle_ES1", ObjectHelper.SkipConstructor<Settlement>()));
                break;
            case "resolved-battle": test.Assault._battleState = BattleState.AttackerVictory; break;
        }

        Assert.False(test.Fixture.EndMissions().Succeeded);
        Assert.False(test.Fixture.Restore().Succeeded);
        Assert.Empty(test.ExitRequests);
        Assert.Equal(0, test.FinalizeRequests);
        Assert.Equal(0, test.BreakRequests);
    }

    [Theory]
    [InlineData("removed")]
    [InlineData("rebound")]
    [InlineData("unchanged")]
    public void Cleanup_BesiegerTargetIdentity_IsCheckedBeforeMutationButDoesNotBlockMissionExit(string drift)
    {
        using var test = new AssaultFixture();
        var target = AssaultFixture.CreateParty("context-target");
        Assert.True(test.Objects.AddExisting("context-target", target));
        test.SetOriginalTarget(target);
        if (drift != "unchanged") Assert.True(test.Objects.Remove(target));
        if (drift == "rebound")
            Assert.True(test.Objects.AddExisting("context-target", AssaultFixture.CreateParty("replacement")));

        Assert.True(test.Fixture.EndMissions().Succeeded);
        Assert.Equal(2, test.ExitRequests.Count);
        Assert.Equal(drift == "unchanged", test.Fixture.Restore().Succeeded);
        Assert.Equal(drift == "unchanged" ? 1 : 0, test.FinalizeRequests);
        Assert.Equal(drift == "unchanged" ? 1 : 0, test.BreakRequests);
        Assert.Equal(drift == "unchanged", test.Fixture.Verify().Succeeded);
    }

    internal sealed class AssaultFixture : IDisposable
    {
        private readonly Campaign previousCampaign;
        private readonly bool previousServer;
        public readonly Dictionary<string, Player> Players = new();
        public readonly Dictionary<NetPeer, string> PeerOwners = new();
        public readonly HashSet<string> Disconnected = new();
        public readonly List<NetPeer> ExitRequests = new();
        public readonly global::GameInterface.Services.ObjectManager.ObjectManager Objects;
        private readonly TestMessageBroker broker = new();
        private readonly Mock<IMobilePartyBehaviorSnapshot> behavior = new();
        private IDisposable visibilityHandler;
        public readonly DefenderSiegeContextFixture Fixture;
        public readonly Settlement Settlement;
        public readonly MobileParty[] Defenders;
        public readonly MapEvent Assault;
        public readonly Mock<ISiegeEventInterface> Siege = new();
        public bool StartAttempted => Read<bool>("startAttempted");
        public bool HasCreatedBesiegerOwnership => Read<bool>("createdBesieger");
        public MobileParty CapturedBesieger => Read<MobileParty>("besieger");
        public Hero CreatedBesiegerHero => Read<Hero>("createdBesiegerHero");
        public Clan CreatedBesiegerClan => Read<Clan>("createdBesiegerClan");
        public int FinalizeRequests;
        public int BreakRequests;

        public AssaultFixture(Settlement existingSettlement = null, MobileParty[] existingDefenders = null,
            global::GameInterface.Services.ObjectManager.ObjectManager existingObjects = null,
            Dictionary<string, Player> existingPlayers = null)
        {
            GameBootStrap.Initialize();
            previousCampaign = Campaign.Current;
            previousServer = ModInformation.IsServer;
            try
            {
                ModInformation.IsServer = true;
                var players = new Mock<IPlayerManager>();
                var siege = Siege;
                Objects = existingObjects ?? new(Serilog.Core.Logger.None);
                var network = new Mock<INetwork>();
                Fixture = new DefenderSiegeContextFixture(Objects, players.Object, behavior.Object, siege.Object, broker, network.Object, new DefenderFixtureBehaviorIdentity(Objects));
                Settlement = existingSettlement ?? ObjectHelper.SkipConstructor<Settlement>();
                if (existingSettlement == null)
                {
                    Settlement.StringId = "castle_ES1";
                    Settlement.Party = ObjectHelper.SkipConstructor<PartyBase>();
                    Settlement.Party.Settlement = Settlement;
                }
                var besieger = CreateParty("besieger");
                Defenders = existingDefenders ?? new[] { CreateParty("party_testclient"), CreateParty("party_testclient2") };
                var peers = new Dictionary<string, NetPeer>();
                var defenderPlayers = Read<Dictionary<string, Player>>("defenderPlayers");
                var defenderParties = Read<Dictionary<string, MobileParty>>("defenderParties");
                var defenderPeers = Read<Dictionary<string, NetPeer>>("defenderPeers");
                for (int index = 0; index < Defenders.Length; index++)
                {
                    string id = index == 0 ? "testclient" : "testclient2";
                    var player = existingPlayers != null ? existingPlayers[id] :
                        new Player(id, "hero_" + id, Defenders[index].StringId, "clan_" + id, "character_" + id);
                    var peer = new TestNetwork().CreatePeer($"127.0.0.{index + 1}");
                    Players.Add(id, player);
                    peers.Add(id, peer);
                    PeerOwners.Add(peer, id);
                    defenderPlayers.Add(id, player);
                    defenderParties.Add(id, Defenders[index]);
                    defenderPeers.Add(id, peer);
                    if (existingObjects == null) Assert.True(Objects.AddExisting(player.MobilePartyId, Defenders[index]));
                }
                players.SetupGet(value => value.Players).Returns(() => Players.Values.ToArray());
                players.Setup(value => value.IsConnected(It.IsAny<Player>())).Returns((Player player) => !Disconnected.Contains(player.ControllerId));
                players.Setup(value => value.TryGetPlayer(It.IsAny<string>(), out It.Ref<Player>.IsAny))
                    .Returns((string id, out Player player) => Players.TryGetValue(id, out player));
                players.Setup(value => value.TryGetPeer(It.IsAny<string>(), out It.Ref<NetPeer>.IsAny))
                    .Returns((string id, out NetPeer peer) => peers.TryGetValue(id, out peer));
                players.Setup(value => value.TryGetPlayer(It.IsAny<NetPeer>(), out It.Ref<Player>.IsAny))
                    .Returns((NetPeer peer, out Player player) => Players.TryGetValue(PeerOwners[peer], out player));
                if (existingObjects == null) Assert.True(Objects.AddExisting("castle_ES1", Settlement));
                Assert.True(Objects.AddExisting("besieger", besieger));
                Assault = ObjectHelper.SkipConstructor<MapEvent>();
                Assault._mapEventType = MapEvent.BattleTypes.Siege;
                Assault._battleState = BattleState.None;
                Assault.MapEventSettlement = Settlement;
                var defenderSide = new MapEventSide(Assault, BattleSideEnum.Defender, Settlement.Party);
                var attackerSide = new MapEventSide(Assault, BattleSideEnum.Attacker, besieger.Party);
                AccessTools.Field(typeof(MapEvent), "_sides").SetValue(Assault, new[] { defenderSide, attackerSide });
                AddParty(defenderSide, Settlement.Party);
                foreach (var party in Defenders) AddParty(defenderSide, party.Party);
                AddParty(attackerSide, besieger.Party);
                Assert.True(Objects.AddExisting("fixture-assault", Assault));
                var createdSiege = ObjectHelper.SkipConstructor<SiegeEvent>();
                var camp = ObjectHelper.SkipConstructor<BesiegerCamp>();
                AccessTools.Field(typeof(BesiegerCamp), "_besiegerParties").SetValue(camp, new MBList<MobileParty> { besieger });
                AccessTools.Field(typeof(SiegeEvent), nameof(SiegeEvent.BesiegerCamp)).SetValue(createdSiege, camp);
                Settlement.SiegeEvent = createdSiege;
                Write("campaign", Campaign.Current);
                Write("settlement", Settlement);
                Write("besieger", besieger);
                Write("partyId", "besieger");
                Write("createdSiege", createdSiege);
                Write("startAttempted", true);
                Write("position", besieger.Position);
                Write("originalBehaviorReferences", new DefenderFixtureBehaviorIdentity(Objects).Capture(besieger, default));
                foreach (var party in Defenders.Select(value => value.Party).Concat(new[] { Settlement.Party, besieger.Party }))
                    Read<HashSet<PartyBase>>("expectedBattleParties").Add(party);
                network.Setup(value => value.Send(It.IsAny<NetPeer>(), It.IsAny<IMessage>()))
                    .Callback((NetPeer peer, IMessage message) =>
                    {
                        if (message is NetworkEndLateJoinModeFixtureMission) ExitRequests.Add(peer);
                    });
                broker.Subscribe<NetworkMapEventFinalizeAttempted>(payload =>
                {
                    Assert.Equal("fixture-assault", payload.What.MapEventId);
                    FinalizeRequests++;
                    foreach (var party in Defenders.Select(value => value.Party).Concat(new[] { Settlement.Party, besieger.Party }))
                        party._mapEventSide = null;
                    broker.Publish(this, new MapEventFinalized(Assault));
                });
                if (existingObjects != null)
                {
                    // Coop.Core does not expose this internal handler to GameInterface.Tests.
                    var type = Type.GetType("Coop.Core.Server.Services.Players.Handlers.PlayerPartyVisibilityHandler, Coop.Core", true);
                    visibilityHandler = (IDisposable)Activator.CreateInstance(type, broker, players.Object,
                        Mock.Of<IConnectionCollection>(), Objects, network.Object, siege.Object);
                    players.Setup(value => value.ClearPeer(It.IsAny<NetPeer>())).Callback((NetPeer peer) =>
                        Disconnected.Add(PeerOwners[peer]));
                }
                siege.Setup(value => value.BreakSiege(besieger)).Callback(() =>
                {
                    BreakRequests++;
                    Settlement.SiegeEvent = null;
                });
                behavior.Setup(value => value.TryApply(besieger, It.IsAny<PartyBehaviorUpdateData>(), out It.Ref<IInteractablePoint>.IsAny))
                    .Returns((MobileParty party, PartyBehaviorUpdateData data, out IInteractablePoint point) => { point = null; return true; });
                behavior.Setup(value => value.TryCreate(besieger, out It.Ref<PartyBehaviorUpdateData>.IsAny))
                    .Returns((MobileParty party, out PartyBehaviorUpdateData data) => { data = default; return true; });
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public MobileParty PrepareStartWithVisitor(string kind)
        {
            var besieger = Read<MobileParty>("besieger");
            var besiegerClan = ObjectHelper.SkipConstructor<Clan>();
            var hostileClan = ObjectHelper.SkipConstructor<Clan>();
            hostileClan.IsBanditFaction = true;
            besieger._actualClan = besiegerClan;
            var visitor = CreateParty("uncaptured-visitor");
            visitor._actualClan = kind == "non-hostile" ? besiegerClan : hostileClan;
            visitor.IsActive = kind != "inactive";
            visitor.IsCaravan = kind == "caravan";
            visitor.IsVillager = kind == "villager";
            visitor.IsMilitia = kind == "militia" || kind == "rebellious-militia";
            Settlement.Town = ObjectHelper.SkipConstructor<Town>();
            Settlement.Town.InRebelliousState = kind == "rebellious-militia";
            Settlement._partiesCache = new MBList<MobileParty>(Defenders.Append(visitor));
            Settlement.SiegeEvent = null;
            Settlement.Party._mapEventSide = null;
            besieger.Party._mapEventSide = null;
            foreach (var defender in Defenders)
            {
                defender.Party._mapEventSide = null;
                defender._currentSettlement = Settlement;
            }
            Write("startAttempted", false);
            Write("createdSiege", null);
            Assert.Equal(kind != "non-hostile", visitor.MapFaction.IsAtWarWith(besieger.MapFaction));
            return visitor;
        }

        public void AddUncapturedDefender() => AddParty(Assault.DefenderSide, CreateParty("extra-lord").Party);

        public void SetOriginalTarget(MobileParty target)
        {
            var besieger = Read<MobileParty>("besieger");
            besieger.TargetParty = target;
            var snapshot = new PartyBehaviorUpdateData { TargetPartyId = "context-target" };
            Write("originalBehavior", snapshot);
            Write("originalBehaviorReferences", new DefenderFixtureBehaviorIdentity(Objects).Capture(besieger, snapshot));
            behavior.Setup(value => value.TryCreate(besieger, out It.Ref<PartyBehaviorUpdateData>.IsAny))
                .Returns((MobileParty party, out PartyBehaviorUpdateData data) => { data = snapshot; return true; });
        }

        public void Disconnect(string controllerId)
        {
            NetPeer peer = PeerOwners.Single(entry => entry.Value == controllerId).Key;
            broker.Publish(this, new PlayerDisconnected(peer, default));
            GameThread.Run(() => { }, blocking: true);
        }

        public void PrepareCreatedBesiegerPostCreationException()
        {
            if (Campaign.Current == null) Campaign.Current = ObjectHelper.SkipConstructor<Campaign>();
            Settlement.SiegeEvent = null;
            Settlement.Party._mapEventSide = null;
            foreach (var defender in Defenders)
            {
                defender._currentSettlement = Settlement;
                defender.Party._mapEventSide = null;
                defender._actualClan = ObjectHelper.SkipConstructor<Clan>();
            }
            Write("campaign", null);
            Write("settlement", null);
            Write("besieger", null);
            Write("partyId", null);
            Write("createdBesieger", false);
            Write("createdBesiegerCleanupStarted", false);
            Write("createdBesiegerHero", null);
            Write("createdBesiegerClan", null);
            Write("startAttempted", false);
            Write("restored", false);

            var party = CreateParty("created-post-creation-besieger");
            var hero = ObjectHelper.SkipConstructor<Hero>();
            var clan = new Clan();
            clan._companionsCache.Add(hero);
            hero._companionOf = clan;
            hero.DeathMark = KillCharacterAction.KillCharacterActionDetail.None;
            var component = ObjectHelper.SkipConstructor<LordPartyComponent>();
            component._leader = hero;
            party._partyComponent = component;
            party._actualClan = clan;
            Assert.True(Objects.AddExisting(party.StringId, party));
            behavior.Setup(value => value.TryCreate(party, out It.Ref<PartyBehaviorUpdateData>.IsAny))
                .Callback(() => postCreationSnapshotAttempted = true)
                .Throws<InvalidOperationException>();
            createdBesiegerCandidate = party;
            createdBesiegerHero = hero;
            createdBesiegerClan = clan;
            postCreationSnapshotAttempted = false;
        }

        public int PrepareCreatedBesiegerCleanupRetry(bool ownsClanLeader = false)
        {
            var party = CreateParty("destroyed-created-besieger");
            var hero = ObjectHelper.SkipConstructor<Hero>();
            var clan = new Clan();
            var existingCompanion = ObjectHelper.SkipConstructor<Hero>();
            clan._companionsCache.Add(existingCompanion);
            existingCompanion._companionOf = clan;
            int companionBaseline = clan.Companions.Count();
            clan._companionsCache.Add(hero);
            hero._companionOf = clan;
            hero.DeathMark = KillCharacterAction.KillCharacterActionDetail.None;
            var component = ObjectHelper.SkipConstructor<LordPartyComponent>();
            component._leader = hero;
            party._partyComponent = component;
            party._actualClan = clan;
            Assert.True(Objects.AddExisting(party.StringId, party));
            Settlement.SiegeEvent = null;
            Settlement.Party._mapEventSide = null;
            Write("besieger", party);
            Write("partyId", party.StringId);
            Write("createdBesieger", true);
            Write("createdBesiegerCleanupStarted", true);
            Write("createdBesiegerHero", hero);
            Write("createdBesiegerClan", clan);
            Write("relationRestored", true);
            Write("restored", false);
            if (ownsClanLeader)
            {
                clan.IsBanditFaction = true;
                clan.SetLeader(hero);
                Write("createdBesiegerOwnsClanLeader", true);
            }
            return companionBaseline;
        }

        private static void AddParty(MapEventSide side, PartyBase party)
        {
            var member = ObjectHelper.SkipConstructor<MapEventParty>();
            member.Party = party;
            side._battleParties.Add(member);
            party._mapEventSide = side;
        }

        internal static MobileParty CreateParty(string id)
        {
            var party = ObjectHelper.SkipConstructor<MobileParty>();
            party.StringId = id;
            party.Party = ObjectHelper.SkipConstructor<PartyBase>();
            party.Party.MobileParty = party;
            party.Ai = new MobilePartyAi(party);
            party.IsActive = true;
            party._position = new CampaignVec2(new Vec2(12f, 24f), true);
            return party;
        }

        private T Read<T>(string name) => (T)AccessTools.Field(typeof(DefenderSiegeContextFixture), name).GetValue(Fixture);
        private void Write(string name, object value) => AccessTools.Field(typeof(DefenderSiegeContextFixture), name).SetValue(Fixture, value);
        public void Dispose()
        {
            visibilityHandler?.Dispose();
            Campaign.Current = previousCampaign;
            ModInformation.IsServer = previousServer;
        }
    }
}
#endif
