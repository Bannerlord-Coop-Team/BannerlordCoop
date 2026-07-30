using Common;
using Common.Messaging;
using Common.Network;
using Common.Util;
using Coop.Core.Client.Messages;
using Coop.Core.Client.Services.MobileParties.Messages;
using Coop.Core.Client.Services.SiegeEvents.Messages;
using Coop.Core.Common.Services.SiegeEvents;
using Coop.Core.Server.Connections.Messages;
using Coop.Core.Server.Services.MobileParties.Messages;
using Coop.Core.Server.Services.SiegeEvents.Messages;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Services.MapEvents;
using E2E.Tests.Util;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.Kingdoms;
using GameInterface.Services.Settlements.Interfaces;
using GameInterface.Services.SiegeEvents.Interfaces;
using GameInterface.Services.SiegeEvents.Messages;
using GameInterface.Services.SiegeEvents.Validation;
using GameInterface.Services.Villages.Interfaces;
using GameInterface.Utils;
using HarmonyLib;
using System.Reflection;
using System.Threading;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;
using Xunit.Abstractions;

namespace E2E.Tests.Services.SiegeEvents;

public class SiegeEntryValidationFlowTests : MapEventTestBase
{
    private static IReadOnlyList<MethodBase> SettlementEncounterDisabledMethods => new[]
    {
        AccessTools.Method(
            typeof(SettlementInterface),
            nameof(SettlementInterface.PartyEnterSettlement)),
        AccessTools.Method(
            typeof(SettlementInterface),
            nameof(SettlementInterface.StartSettlementEncounter)),
    };

    private static IReadOnlyList<MethodBase> SiegeStartDisabledMethods => new[]
    {
        AccessTools.Method(
            typeof(SiegeEventInterface),
            nameof(SiegeEventInterface.StartSiegeEvent)),
    };

    private static IReadOnlyList<MethodBase> SiegeJoinDisabledMethods => new[]
    {
        AccessTools.Method(
            typeof(SiegeEventInterface),
            nameof(SiegeEventInterface.JoinSiegeCamp)),
    };

    private static IReadOnlyList<MethodBase> SiegeCreationDisabledMethods => new[]
    {
        AccessTools.Method(
            typeof(MobileParty),
            nameof(MobileParty.OnPartyJoinedSiegeInternal)),
        AccessTools.Method(
            typeof(BesiegerCamp),
            nameof(BesiegerCamp.InitializeSiegeEventSide)),
        AccessTools.Method(
            typeof(Settlement),
            nameof(Settlement.InitializeSiegeEventSide)),
    };

    private static IReadOnlyList<MethodBase> SettlementLeaveSideEffectDisabledMethods => new[]
    {
        AccessTools.Method(
            typeof(SettlementComponent),
            nameof(SettlementComponent.OnPartyLeft)),
        AccessTools.Method(
            typeof(CampaignEventDispatcher),
            nameof(CampaignEventDispatcher.OnSettlementLeft)),
    };

    public SiegeEntryValidationFlowTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void BesiegeRequest_FromPeerThatDoesNotControlParty_IsRejected()
    {
        var ownerClient = Clients.First();
        var otherClient = Clients.Last();
        var context = CreateEntryContext(ownerClient, "Owner");
        Server.NetworkSentMessages.Clear();

        otherClient.Call(() => otherClient.Resolve<INetwork>().SendAll(
            new NetworkRequestBesiegeSettlement(
                context.PartyId,
                context.SettlementId,
                "wrong-peer")));

        var result = Assert.Single(
            Server.NetworkSentMessages.GetMessages<NetworkSiegeEntryResult>());
        Assert.Equal(SiegeEntryOutcome.Rejected, result.Outcome);
        Assert.Equal(SiegeEntryDenialReason.InvalidRequester, result.Reason);
        Assert.Equal(SiegeEntryDisposition.Map, result.Disposition);
    }

    [Fact]
    public void BesiegeRequest_AfterPartyTargetsAnotherSettlement_IsRejected()
    {
        var client = Clients.First();
        var context = CreateEntryContext(client, "PlayerOne");
        RequestSettlementInteraction(client, context);
        var otherSettlementId = TestEnvironment.CreateRegisteredObject<Settlement>();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(
                context.PartyId,
                out var party));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(
                otherSettlementId,
                out var otherSettlement));

            using (new AllowedThread())
            {
                otherSettlement.GatePosition = Position(900f, 900f);
                party.SetMoveGoToSettlement(
                    otherSettlement,
                    MobileParty.NavigationType.Default,
                    isTargetingThePort: false);
                party.Position = otherSettlement.GatePosition;
            }
        });
        Server.NetworkSentMessages.Clear();

        SendBesiegeAttempt(client, context);

        var result = Assert.Single(
            Server.NetworkSentMessages.GetMessages<NetworkSiegeEntryResult>());
        Assert.Equal(SiegeEntryOutcome.Rejected, result.Outcome);
        Assert.Equal(SiegeEntryDenialReason.MovementTargetMismatch, result.Reason);
        Assert.Equal(SiegeEntryDisposition.Map, result.Disposition);
        Assert.Null(result.CanonicalSettlementId);
    }

    [Fact]
    public void BesiegeRequest_AfterTargetedPartyMovesTooFar_IsRejected()
    {
        var client = Clients.First();
        var context = CreateEntryContext(client, "PlayerOne");
        RequestSettlementInteraction(client, context);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(
                context.PartyId,
                out var party));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(
                context.SettlementId,
                out var settlement));

            using (new AllowedThread())
            {
                party.Position = Position(900f, 900f);
            }

            Assert.Same(settlement, party.TargetSettlement);
        });
        Server.NetworkSentMessages.Clear();

        SendBesiegeAttempt(client, context);

        var result = Assert.Single(
            Server.NetworkSentMessages.GetMessages<NetworkSiegeEntryResult>());
        Assert.Equal(SiegeEntryOutcome.Rejected, result.Outcome);
        Assert.Equal(SiegeEntryDenialReason.TooFar, result.Reason);
        Assert.Equal(SiegeEntryDisposition.Map, result.Disposition);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(
                context.PartyId,
                out var party));
            Assert.Equal(MoveModeType.Hold, party.PartyMoveMode);
        });
    }

    [Theory]
    [InlineData(true, SiegeEntryDenialReason.ActiveMapEvent)]
    [InlineData(false, SiegeEntryDenialReason.ConflictingSiege)]
    public void BesiegeRequest_AfterPartyEntersOtherActivity_IsRejected(
        bool addMapEvent,
        SiegeEntryDenialReason expectedReason)
    {
        var client = Clients.First();
        var context = CreateEntryContext(client, "PlayerOne");
        RequestSettlementInteraction(client, context);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(
                context.PartyId,
                out var party));

            using (new AllowedThread())
            {
                if (addMapEvent)
                {
                    var mapEventSide = new MapEventSide(
                        ObjectHelper.SkipConstructor<MapEvent>(),
                        BattleSideEnum.Attacker,
                        party.Party);
                    party.Party._mapEventSide = mapEventSide;
                }
                else
                {
                    party._besiegerCamp = ObjectHelper.SkipConstructor<BesiegerCamp>();
                }
            }
        });
        Server.NetworkSentMessages.Clear();

        SendBesiegeAttempt(client, context);

        var result = Assert.Single(
            Server.NetworkSentMessages.GetMessages<NetworkSiegeEntryResult>());
        Assert.Equal(SiegeEntryOutcome.Rejected, result.Outcome);
        Assert.Equal(expectedReason, result.Reason);
    }

    [Fact]
    public void BesiegeRequest_ForOwnSettlement_IsRejectedAsDefender()
    {
        var client = Clients.First();
        var context = CreateEntryContext(client, "PlayerOne");

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(
                context.PartyId,
                out var party));
            Assert.True(Server.ObjectManager.TryGetObject<Town>(
                context.TownId,
                out var town));

            using (new AllowedThread())
            {
                town.OwnerClan = party.ActualClan;
            }
        });

        RequestSettlementInteraction(client, context);
        Server.NetworkSentMessages.Clear();

        SendBesiegeAttempt(client, context);

        var result = Assert.Single(
            Server.NetworkSentMessages.GetMessages<NetworkSiegeEntryResult>());
        Assert.Equal(SiegeEntryOutcome.Rejected, result.Outcome);
        Assert.Equal(SiegeEntryDenialReason.DefenderDisposition, result.Reason);
        Assert.Equal(SiegeEntryDisposition.Map, result.Disposition);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(
                context.SettlementId,
                out var settlement));
            Assert.Null(settlement.SiegeEvent);
        });
    }

    [Fact]
    public void BesiegeRequest_AfterFactionsMakePeace_IsRejected()
    {
        var client = Clients.First();
        var context = CreateEntryContext(client, "PlayerOne");
        RequestSettlementInteraction(client, context);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(
                context.PartyId,
                out var party));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(
                context.SettlementId,
                out var settlement));

            MakePeaceAction.Apply(party.ActualClan, settlement.OwnerClan);
            Assert.False(FactionManager.IsAtWarAgainstFaction(
                party.MapFaction,
                settlement.MapFaction));
        });
        Server.NetworkSentMessages.Clear();

        SendBesiegeAttempt(client, context);

        var result = Assert.Single(
            Server.NetworkSentMessages.GetMessages<NetworkSiegeEntryResult>());
        Assert.Equal(SiegeEntryOutcome.Rejected, result.Outcome);
        Assert.Equal(SiegeEntryDenialReason.InvalidFaction, result.Reason);
        Assert.Equal(SiegeEntryDisposition.Map, result.Disposition);
    }

    [Fact]
    public void BesiegeRequest_WithValidGrantAndAttackerState_IsAppliedOnce()
    {
        var client = Clients.First();
        var context = CreateEntryContext(client, "PlayerOne");
        var interactionId = RequestSettlementInteraction(client, context);
        Server.NetworkSentMessages.Clear();

        SendBesiegeAttempt(client, context, SiegeStartDisabledMethods);

        var firstResult = Assert.Single(
            Server.NetworkSentMessages.GetMessages<NetworkSiegeEntryResult>());
        Assert.Equal(SiegeEntryOutcome.Applied, firstResult.Outcome);
        Assert.Equal(SiegeEntryDenialReason.None, firstResult.Reason);
        Assert.Equal(interactionId, firstResult.InteractionId);
        Assert.Equal(
            interactionId,
            Assert.Single(
                client.NetworkSentMessages.GetMessages<NetworkRequestBesiegeSettlement>())
                .InteractionId);

        Server.NetworkSentMessages.Clear();
        client.NetworkSentMessages.Clear();

        SendBesiegeAttempt(client, context);

        var replayResult = Assert.Single(
            Server.NetworkSentMessages.GetMessages<NetworkSiegeEntryResult>());
        Assert.Equal(SiegeEntryOutcome.Rejected, replayResult.Outcome);
        Assert.Equal(
            SiegeEntryDenialReason.MissingInteractionGrant,
            replayResult.Reason);
        Assert.Null(
            Assert.Single(
                client.NetworkSentMessages.GetMessages<NetworkRequestBesiegeSettlement>())
                .InteractionId);
    }

    [Fact]
    public void BesiegeRequest_AfterSettlementLeave_IsRejectedAsStale()
    {
        var client = Clients.First();
        var context = CreateEntryContext(client, "PlayerOne");
        var interactionId = RequestSettlementInteraction(client, context);

        client.Call(
            () => client.Resolve<INetwork>().SendAll(
                new NetworkRequestEndSettlementEncounter(context.PartyId)),
            new[]
            {
                AccessTools.Method(
                    typeof(SettlementInterface),
                    nameof(SettlementInterface.PartyLeaveSettlement)),
            });
        Server.NetworkSentMessages.Clear();

        client.Call(() => client.Resolve<INetwork>().SendAll(
            new NetworkRequestBesiegeSettlement(
                context.PartyId,
                context.SettlementId,
                interactionId)));

        var result = Assert.Single(
            Server.NetworkSentMessages.GetMessages<NetworkSiegeEntryResult>());
        Assert.Equal(SiegeEntryOutcome.Rejected, result.Outcome);
        Assert.Equal(
            SiegeEntryDenialReason.MissingInteractionGrant,
            result.Reason);
    }

    [Fact]
    public void QueuedGrantConsumptionBeforeSettlementLeave_ConsumesTheGrantFirst()
    {
        var client = Clients.First();
        var context = CreateEntryContext(client, "PlayerOne");
        var interactionId = RequestSettlementInteraction(client, context);
        var grantStore = Server.Resolve<ISiegeInteractionGrantStore>();
        bool grantConsumed = false;
        Exception workerException = null;

        var networkThread = new Thread(() =>
        {
            try
            {
                GameThread.RunSafe(() =>
                    grantConsumed = grantStore.TryConsume(
                        client.NetPeer,
                        interactionId,
                        context.PartyId,
                        context.SettlementId,
                        presentedCamp: null));
                Server.SimulateMessage(
                    client.NetPeer,
                    new NetworkRequestEndSettlementEncounter(context.PartyId));
            }
            catch (Exception exception)
            {
                workerException = exception;
            }
        });
        networkThread.Start();
        Assert.True(networkThread.Join(TimeSpan.FromSeconds(5)));
        Assert.Null(workerException);

        Server.Call(() => GameThread.Instance.Update(TimeSpan.Zero));

        Assert.True(grantConsumed);
    }

    [Fact]
    public void AuthoritativePartyLeave_RevokesThePartyInteractionGrant()
    {
        var client = Clients.First();
        var context = CreateEntryContext(client, "PlayerOne");
        var interactionId = RequestSettlementInteraction(client, context);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(
                context.PartyId,
                out var party));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(
                context.SettlementId,
                out var settlement));

            using (new AllowedThread())
            {
                party.CurrentSettlement = settlement;
            }
            LeaveSettlementAction.ApplyForParty(party);

            Assert.False(
                Server.Resolve<ISiegeInteractionGrantStore>().TryConsume(
                    client.NetPeer,
                    interactionId,
                    context.PartyId,
                    context.SettlementId,
                    presentedCamp: null));
        }, SettlementLeaveSideEffectDisabledMethods);
    }

    [Fact]
    public void SuppressedAuthoritativePartyLeave_PreservesThePartyInteractionGrant()
    {
        var client = Clients.First();
        var context = CreateEntryContext(client, "PlayerOne");
        var interactionId = RequestSettlementInteraction(client, context);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(
                context.PartyId,
                out var party));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(
                context.SettlementId,
                out var settlement));
            Server.Resolve<IKingdomCreationSettlementTracker>().Track(
                context.PartyId,
                context.SettlementId);

            using (new AllowedThread())
            {
                party.CurrentSettlement = settlement;
            }
            LeaveSettlementAction.ApplyForParty(party);

            Assert.True(
                Server.Resolve<ISiegeInteractionGrantStore>().TryConsume(
                    client.NetPeer,
                    interactionId,
                    context.PartyId,
                    context.SettlementId,
                    presentedCamp: null));
        }, SettlementLeaveSideEffectDisabledMethods);
    }

    [Fact]
    public void AuthoritativePartyLeave_WhenApplyThrowsAfterMutation_RevokesThePartyInteractionGrant()
    {
        var client = Clients.First();
        var context = CreateEntryContext(client, "PlayerOne");
        var interactionId = RequestSettlementInteraction(client, context);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(
                context.PartyId,
                out var party));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(
                context.SettlementId,
                out var settlement));

            using (new AllowedThread())
            {
                party.CurrentSettlement = settlement;
            }

            using var throwingPatch = new ThrowingPatchScope(
                AccessTools.Method(
                    typeof(CampaignEventDispatcher),
                    nameof(CampaignEventDispatcher.OnSettlementLeft)));
            Assert.Throws<InvalidOperationException>(
                () => LeaveSettlementAction.ApplyForParty(party));

            Assert.Null(party.CurrentSettlement);
            Assert.False(
                Server.Resolve<ISiegeInteractionGrantStore>().TryConsume(
                    client.NetPeer,
                    interactionId,
                    context.PartyId,
                    context.SettlementId,
                    presentedCamp: null));
        });
    }

    [Fact]
    public void JoinRequest_WithValidGrantAndAttackerState_IsApplied()
    {
        var client = Clients.First();
        var context = CreateEntryContext(client, "PlayerOne");

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(
                context.PartyId,
                out var joiningParty));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(
                context.SettlementId,
                out var settlement));

            using (new AllowedThread())
            {
                var siegeLeader = GameObjectCreator.CreateInitializedObject<MobileParty>();
                siegeLeader.ActualClan = joiningParty.ActualClan;
                siegeLeader.LeaderHero.Clan = joiningParty.ActualClan;
                var ownerClan = settlement.Town.OwnerClan;
                settlement.Town.OwnerClan = null;
                var siegeEvent = new SiegeEvent(settlement, siegeLeader);
                settlement.Town.OwnerClan = ownerClan;
                siegeEvent.BesiegerCamp._besiegerParties.Add(siegeLeader);
                siegeEvent.BesiegerCamp._leaderParty = siegeLeader;

                Assert.Same(joiningParty.MapFaction, siegeLeader.MapFaction);
                Assert.Same(ownerClan, settlement.Party.MapFaction);
                Assert.True(
                    settlement.Party.MapFaction.IsAtWarWith(
                        joiningParty.MapFaction));
                Assert.True(
                    siegeEvent.CanPartyJoinSide(
                        joiningParty.Party,
                        BattleSideEnum.Attacker));
            }

            var validation = Server.Resolve<ISiegeEntryValidator>().ValidateEntry(
                joiningParty,
                settlement,
                SiegeEntryAction.Join);
            Assert.True(validation.IsValid, validation.Reason.ToString());
        }, SiegeCreationDisabledMethods);

        var interactionId = RequestSettlementInteraction(client, context);
        Server.NetworkSentMessages.Clear();

        SendJoinAttempt(client, context, SiegeJoinDisabledMethods);

        var result = Assert.Single(
            Server.NetworkSentMessages.GetMessages<NetworkSiegeEntryResult>());
        Assert.Equal(SiegeEntryRequestType.Join, result.RequestType);
        Assert.Equal(SiegeEntryOutcome.Applied, result.Outcome);
        Assert.Equal(SiegeEntryDenialReason.None, result.Reason);
        Assert.Equal(interactionId, result.InteractionId);
        Assert.Equal(
            interactionId,
            Assert.Single(
                client.NetworkSentMessages.GetMessages<NetworkRequestJoinSiegeCamp>())
                .InteractionId);
    }

    [Fact]
    public void JoinRequest_AfterPresentedCampIsReplaced_IsRejected()
    {
        var client = Clients.First();
        var context = CreateEntryContext(client, "PlayerOne");
        BesiegerCamp presentedCamp = null;

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(
                context.PartyId,
                out var party));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(
                context.SettlementId,
                out var settlement));

            using (new AllowedThread())
            {
                presentedCamp = CreatePresentedSiege(party, settlement);
            }
        }, SiegeCreationDisabledMethods);

        var interactionId = RequestSettlementInteraction(client, context);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(
                context.PartyId,
                out var party));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(
                context.SettlementId,
                out var settlement));

            using (new AllowedThread())
            {
                var replacementCamp = CreatePresentedSiege(party, settlement);
                Assert.NotSame(presentedCamp, replacementCamp);
                Assert.Same(replacementCamp, settlement.SiegeEvent.BesiegerCamp);
            }
        }, SiegeCreationDisabledMethods);
        Server.NetworkSentMessages.Clear();

        client.Call(() => client.Resolve<INetwork>().SendAll(
            new NetworkRequestJoinSiegeCamp(
                context.PartyId,
                context.SettlementId,
                interactionId)));

        var result = Assert.Single(
            Server.NetworkSentMessages.GetMessages<NetworkSiegeEntryResult>());
        Assert.Equal(SiegeEntryOutcome.Rejected, result.Outcome);
        Assert.Equal(
            SiegeEntryDenialReason.MissingInteractionGrant,
            result.Reason);
    }

    [Theory]
    [InlineData(MapEvent.BattleTypes.Siege, false)]
    [InlineData(MapEvent.BattleTypes.SallyOut, false)]
    [InlineData(MapEvent.BattleTypes.SiegeOutside, false)]
    [InlineData(MapEvent.BattleTypes.BlockadeBattle, false)]
    [InlineData(MapEvent.BattleTypes.BlockadeSallyOutBattle, false)]
    [InlineData(MapEvent.BattleTypes.FieldBattle, true)]
    public void ReloadedBesieger_WithSiegeRelatedMapEvent_IsValidAndRestoresEncounter(
        MapEvent.BattleTypes battleType,
        bool isSiegeAmbush)
    {
        var client = Clients.First();
        var context = CreateEntryContext(client, "PlayerOne");
        BesiegerCamp expectedCamp = null;

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(
                context.PartyId,
                out var party));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(
                context.SettlementId,
                out var settlement));

            using (new AllowedThread())
            {
                expectedCamp = CreatePresentedSiege(party, settlement);
                expectedCamp._besiegerParties.Add(party);
                party._besiegerCamp = expectedCamp;

                var mapEvent = GameObjectCreator.CreateInitializedObject<MapEvent>();
                mapEvent._mapEventType = battleType;
                mapEvent.MapEventSettlement = settlement;
                if (isSiegeAmbush)
                {
                    mapEvent.Component =
                        ObjectHelper.SkipConstructor<SiegeAmbushEventComponent>();
                }

                var attackerSide = new MapEventSide(
                    mapEvent,
                    BattleSideEnum.Attacker,
                    party.Party);
                var defenderSide = new MapEventSide(
                    mapEvent,
                    BattleSideEnum.Defender,
                    settlement.Party);
                mapEvent._sides[(int)BattleSideEnum.Attacker] = attackerSide;
                mapEvent._sides[(int)BattleSideEnum.Defender] = defenderSide;
                party.Party._mapEventSide = attackerSide;
                settlement.Party._mapEventSide = defenderSide;
            }

            var validation = Server.Resolve<ISiegeEntryValidator>()
                .ValidateReloadedBesieger(party);
            Assert.True(validation.IsValid, validation.Reason.ToString());
            Assert.Same(expectedCamp, party.BesiegerCamp);
        }, SiegeCreationDisabledMethods);

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(
                context.PartyId,
                out var party));
            Assert.True(client.ObjectManager.TryGetObject<Settlement>(
                context.SettlementId,
                out var settlement));
            Assert.True(client.ObjectManager.TryGetObject<Town>(
                context.TownId,
                out var town));

            var previousMainParty = Campaign.Current.MainParty;
            var previousEncounter = Campaign.Current.PlayerEncounter;
            MapEvent mapEvent;
            try
            {
                using (new AllowedThread())
                {
                    Campaign.Current.PlayerEncounter = null;
                    Campaign.Current.MainParty = party;
                    settlement.Town = town;
                    settlement.SetSettlementComponent(town);
                    settlement.Party = new PartyBase(settlement);
                    town.OwnerClan = null;

                    var siegeEvent = new SiegeEvent(settlement, party);
                    siegeEvent.BesiegerCamp._besiegerParties.Add(party);
                    siegeEvent.BesiegerCamp._leaderParty = party;
                    party._besiegerCamp = siegeEvent.BesiegerCamp;

                    mapEvent = GameObjectCreator.CreateInitializedObject<MapEvent>();
                    mapEvent._mapEventType = battleType;
                    mapEvent.MapEventSettlement = settlement;
                    if (isSiegeAmbush)
                    {
                        mapEvent.Component =
                            ObjectHelper.SkipConstructor<SiegeAmbushEventComponent>();
                    }

                    var attackerSide = new MapEventSide(
                        mapEvent,
                        BattleSideEnum.Attacker,
                        party.Party);
                    var defenderSide = new MapEventSide(
                        mapEvent,
                        BattleSideEnum.Defender,
                        settlement.Party);
                    mapEvent._sides[(int)BattleSideEnum.Attacker] = attackerSide;
                    mapEvent._sides[(int)BattleSideEnum.Defender] = defenderSide;
                    party.Party._mapEventSide = attackerSide;
                    settlement.Party._mapEventSide = defenderSide;
                }

                client.Resolve<ISiegeEventInterface>().ReconcileSiegeEntry(
                    SiegeEntryDisposition.Besieger,
                    settlement);

                Assert.NotNull(PlayerEncounter.Current);
                Assert.Same(mapEvent, PlayerEncounter.Current._mapEvent);
                Assert.Same(
                    settlement,
                    PlayerEncounter.Current.EncounterSettlementAux);
            }
            finally
            {
                using (new AllowedThread())
                {
                    Campaign.Current.PlayerEncounter = previousEncounter;
                    Campaign.Current.MainParty = previousMainParty;
                }
            }
        }, SiegeCreationDisabledMethods);
    }

    [Fact]
    public void CampaignReconnect_WithParkedCoherentBesieger_RestoresTheSiege()
    {
        var client = Clients.First();
        var context = CreateEntryContext(client, "PlayerOne");
        BesiegerCamp expectedCamp = null;

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(
                context.PartyId,
                out var party));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(
                context.SettlementId,
                out var settlement));

            using (new AllowedThread())
            {
                expectedCamp = CreatePresentedSiege(party, settlement);
                expectedCamp._besiegerParties.Add(party);
                party._besiegerCamp = expectedCamp;
                party.Position = settlement.GatePosition;
                party.IsActive = false;
            }

            var validation = Server.Resolve<ISiegeEntryValidator>()
                .ValidateReloadedBesieger(party);
            Assert.True(validation.IsValid, validation.Reason.ToString());
        }, SiegeCreationDisabledMethods);
        Server.NetworkSentMessages.Clear();

        Server.Call(() => Server.Resolve<IMessageBroker>().Publish(
            this,
            new PlayerCampaignEntered(client.NetPeer)));

        var result = Assert.Single(
            Server.NetworkSentMessages.GetMessages<NetworkSiegeEntryResult>());
        Assert.Equal(SiegeEntryRequestType.Reconnect, result.RequestType);
        Assert.Equal(SiegeEntryOutcome.Applied, result.Outcome);
        Assert.Equal(SiegeEntryDenialReason.None, result.Reason);
        Assert.Equal(SiegeEntryDisposition.Besieger, result.Disposition);
        Assert.Equal(context.SettlementId, result.CanonicalSettlementId);
        Assert.Empty(
            Server.NetworkSentMessages
                .GetMessages<NetworkClearStaleBesiegerCamp>());

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(
                context.PartyId,
                out var party));
            Assert.True(party.IsActive);
            Assert.Same(expectedCamp, party.BesiegerCamp);
        });
    }

    [Fact]
    public void ReloadedBesieger_WithFinalizedSiegeMapEvent_IsRejected()
    {
        var client = Clients.First();
        var context = CreateEntryContext(client, "PlayerOne");

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(
                context.PartyId,
                out var party));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(
                context.SettlementId,
                out var settlement));

            using (new AllowedThread())
            {
                var camp = CreatePresentedSiege(party, settlement);
                camp._besiegerParties.Add(party);
                party._besiegerCamp = camp;

                var mapEvent = GameObjectCreator.CreateInitializedObject<MapEvent>();
                mapEvent._mapEventType = MapEvent.BattleTypes.Siege;
                mapEvent.MapEventSettlement = settlement;
                mapEvent.State = MapEventState.WaitingRemoval;
                var attackerSide = new MapEventSide(
                    mapEvent,
                    BattleSideEnum.Attacker,
                    party.Party);
                var defenderSide = new MapEventSide(
                    mapEvent,
                    BattleSideEnum.Defender,
                    settlement.Party);
                mapEvent._sides[(int)BattleSideEnum.Attacker] = attackerSide;
                mapEvent._sides[(int)BattleSideEnum.Defender] = defenderSide;
                party.Party._mapEventSide = attackerSide;
                settlement.Party._mapEventSide = defenderSide;
            }

            var validation = Server.Resolve<ISiegeEntryValidator>()
                .ValidateReloadedBesieger(party);

            Assert.False(validation.IsValid);
            Assert.Equal(
                SiegeEntryDenialReason.StaleSiegeLink,
                validation.Reason);
            Assert.Equal(
                SiegeEntryDisposition.Map,
                validation.CanonicalState.Disposition);
        }, SiegeCreationDisabledMethods);
    }

    [Fact]
    public void ReloadedBesieger_WithIncompleteSiegeMapEvent_IsRejected()
    {
        var client = Clients.First();
        var context = CreateEntryContext(client, "PlayerOne");

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(
                context.PartyId,
                out var party));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(
                context.SettlementId,
                out var settlement));

            using (new AllowedThread())
            {
                var camp = CreatePresentedSiege(party, settlement);
                camp._besiegerParties.Add(party);
                party._besiegerCamp = camp;

                var mapEvent = GameObjectCreator.CreateInitializedObject<MapEvent>();
                mapEvent._mapEventType = MapEvent.BattleTypes.Siege;
                mapEvent.MapEventSettlement = settlement;
                party.Party._mapEventSide = new MapEventSide(
                    mapEvent,
                    BattleSideEnum.Attacker,
                    party.Party);
            }

            var validation = Server.Resolve<ISiegeEntryValidator>()
                .ValidateReloadedBesieger(party);

            Assert.False(validation.IsValid);
            Assert.Equal(
                SiegeEntryDenialReason.StaleSiegeLink,
                validation.Reason);
            Assert.Equal(
                SiegeEntryDisposition.Map,
                validation.CanonicalState.Disposition);
        }, SiegeCreationDisabledMethods);
    }

    [Fact]
    public void ReconnectResult_BeforeCampaignEntry_RecomputesFinalSettlementState()
    {
        var client = Clients.First();
        var context = CreateEntryContext(client, "PlayerOne");

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(
                context.PartyId,
                out var party));
            Assert.True(client.ObjectManager.TryGetObject<Settlement>(
                context.SettlementId,
                out var settlement));

            using (new AllowedThread())
            {
                Campaign.Current.MainParty = party;
                settlement.Party = new PartyBase(settlement);
                party._currentSettlement = null;
                if (PlayerEncounter.Current != null)
                    PlayerEncounter.Finish();
            }

            var mapState = Game.Current.GameStateManager.CreateState<MapState>();
            var previousGameStateManager = GameStateManager.Current;
            try
            {
                GameStateManager._current = Game.Current.GameStateManager;
                Game.Current.GameStateManager.CleanAndPushState(mapState);

                client.SimulateMessage(
                    Server.NetPeer,
                    new NetworkSiegeEntryResult(
                        context.PartyId,
                        requestedSettlementId: null,
                        interactionId: null,
                        SiegeEntryRequestType.Reconnect,
                        SiegeEntryOutcome.Rejected,
                        SiegeEntryDenialReason.StaleSiegeLink,
                        SiegeEntryDisposition.Map,
                        canonicalSettlementId: null));
                Assert.Null(PlayerEncounter.Current);

                using (new AllowedThread())
                {
                    party._currentSettlement = settlement;
                }

                client.Resolve<IMessageBroker>().Publish(
                    this,
                    new CampaignEntryCompleted());

                Assert.NotNull(PlayerEncounter.Current);
                Assert.Same(settlement, PlayerEncounter.Current.EncounterSettlementAux);
            }
            finally
            {
                GameStateManager._current = previousGameStateManager;
            }
        });
    }

    [Fact]
    public void CampaignReconnect_WithMalformedBesiegerCamp_RepairsServerAndObservers()
    {
        var client = Clients.First();
        var observer = Clients.Last();
        var context = CreateEntryContext(client, "PlayerOne");
        BesiegerCamp serverCamp = null;
        BesiegerCamp clientCamp = null;
        BesiegerCamp observerCamp = null;
        SiegeEvent observerSiegeEvent = null;

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(
                context.PartyId,
                out var party));
            using (new AllowedThread())
            {
                serverCamp = CreateMalformedCamp(party);
                party._besiegerCamp = serverCamp;
            }
        });
        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(
                context.PartyId,
                out var party));
            using (new AllowedThread())
            {
                clientCamp = CreateMalformedCamp(party);
                party._besiegerCamp = clientCamp;
                Campaign.Current.MainParty = party;
                party.Anchor.IsDisabled = true;
                party.EventPositionAdder = new Vec2(3f, 4f);
            }
        });
        observer.Call(() =>
        {
            Assert.True(observer.ObjectManager.TryGetObject<MobileParty>(
                context.PartyId,
                out var party));
            Assert.True(observer.ObjectManager.TryGetObject<Settlement>(
                context.SettlementId,
                out var settlement));
            Assert.True(observer.ObjectManager.TryGetObject<Town>(
                context.TownId,
                out var town));
            using (new AllowedThread())
            {
                settlement.Town = town;
                settlement.SetSettlementComponent(town);
                settlement.Party = new PartyBase(settlement);
                town.OwnerClan = null;
                observerCamp = CreatePresentedSiege(party, settlement);
                observerCamp._besiegerParties.Clear();
                observerCamp._besiegerParties.Add(party);
                observerCamp._leaderParty = party;
                observerSiegeEvent = observerCamp.SiegeEvent;
                party._besiegerCamp = observerCamp;
            }
        }, SiegeCreationDisabledMethods);
        Server.NetworkSentMessages.Clear();

        Server.Call(() => Server.Resolve<IMessageBroker>().Publish(
            this,
            new PlayerCampaignEntered(client.NetPeer)));

        Assert.Single(
            Server.NetworkSentMessages
                .GetMessages<NetworkClearStaleBesiegerCamp>());
        var result = Assert.Single(
            Server.NetworkSentMessages.GetMessages<NetworkSiegeEntryResult>());
        Assert.Equal(SiegeEntryRequestType.Reconnect, result.RequestType);
        Assert.Equal(SiegeEntryOutcome.Rejected, result.Outcome);
        Assert.Equal(SiegeEntryDenialReason.StaleSiegeLink, result.Reason);
        Assert.Equal(SiegeEntryDisposition.Map, result.Disposition);
        Assert.Null(result.CanonicalSettlementId);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(
                context.PartyId,
                out var party));
            Assert.Null(party.BesiegerCamp);
            Assert.DoesNotContain(party, serverCamp._besiegerParties);
            Assert.Equal(MoveModeType.Hold, party.PartyMoveMode);
        });
        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(
                context.PartyId,
                out var party));
            Assert.Null(party.BesiegerCamp);
            Assert.DoesNotContain(party, clientCamp._besiegerParties);
            Assert.False(party.Anchor.IsDisabled);
            Assert.Equal(Vec2.Zero, party.EventPositionAdder);
        });
        observer.Call(() =>
        {
            Assert.True(observer.ObjectManager.TryGetObject<MobileParty>(
                context.PartyId,
                out var party));
            Assert.True(observer.ObjectManager.TryGetObject<Settlement>(
                context.SettlementId,
                out var settlement));
            Assert.Null(party.BesiegerCamp);
            Assert.DoesNotContain(party, observerCamp._besiegerParties);
            Assert.Same(observerSiegeEvent, settlement.SiegeEvent);
        });
    }

    [Fact]
    public void CampaignReconnect_WithCoherentCampBeyondMaximumDistance_ClearsCamp()
    {
        var client = Clients.First();
        var context = CreateEntryContext(client, "PlayerOne");

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(
                context.PartyId,
                out var party));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(
                context.SettlementId,
                out var settlement));

            using (new AllowedThread())
            {
                var camp = CreatePresentedSiege(party, settlement);
                camp._besiegerParties.Add(party);
                party._besiegerCamp = camp;
                party.Position = settlement.GatePosition;
            }

            var validator = Server.Resolve<ISiegeEntryValidator>();
            var nearValidation = validator.ValidateReloadedBesieger(party);
            Assert.True(nearValidation.IsValid, nearValidation.Reason.ToString());

            using (new AllowedThread())
            {
                party.Position = Position(900f, 900f);
            }

            var farValidation = validator.ValidateReloadedBesieger(party);
            Assert.False(farValidation.IsValid);
            Assert.Equal(SiegeEntryDenialReason.StaleSiegeLink, farValidation.Reason);
        }, SiegeCreationDisabledMethods);
        Server.NetworkSentMessages.Clear();

        Server.Call(
            () => Server.Resolve<IMessageBroker>().Publish(
                this,
                new PlayerCampaignEntered(client.NetPeer)),
            new[]
            {
                AccessTools.Method(
                    typeof(MobileParty),
                    nameof(MobileParty.OnPartyLeftSiegeInternal)),
            });

        var result = Assert.Single(
            Server.NetworkSentMessages.GetMessages<NetworkSiegeEntryResult>());
        Assert.Equal(SiegeEntryOutcome.Rejected, result.Outcome);
        Assert.Equal(SiegeEntryDenialReason.StaleSiegeLink, result.Reason);
        Assert.Equal(SiegeEntryDisposition.Map, result.Disposition);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(
                context.PartyId,
                out var party));
            Assert.Null(party.BesiegerCamp);
            Assert.Equal(MoveModeType.Hold, party.PartyMoveMode);
        });
    }

    private static BesiegerCamp CreatePresentedSiege(
        MobileParty joiningParty,
        Settlement settlement)
    {
        var siegeLeader = GameObjectCreator.CreateInitializedObject<MobileParty>();
        siegeLeader.ActualClan = joiningParty.ActualClan;
        siegeLeader.LeaderHero.Clan = joiningParty.ActualClan;
        var ownerClan = settlement.Town.OwnerClan;
        settlement.Town.OwnerClan = null;
        var siegeEvent = new SiegeEvent(settlement, siegeLeader);
        settlement.Town.OwnerClan = ownerClan;
        siegeEvent.BesiegerCamp._besiegerParties.Add(siegeLeader);
        siegeEvent.BesiegerCamp._leaderParty = siegeLeader;
        return siegeEvent.BesiegerCamp;
    }

    private static BesiegerCamp CreateMalformedCamp(MobileParty party)
    {
        var camp = ObjectHelper.SkipConstructor<BesiegerCamp>();
        ReflectionUtils.SetPrivateField(
            typeof(BesiegerCamp),
            nameof(BesiegerCamp._besiegerParties),
            camp,
            new MBList<MobileParty>());
        camp._besiegerParties.Add(party);
        return camp;
    }

    private EntryContext CreateEntryContext(
        EnvironmentInstance client,
        string controllerId)
    {
        var (_, partyId) = CreatePlayerHeroParty(controllerId);
        TestEnvironment.ConnectRegisteredPlayer(client, controllerId);
        var settlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
        var townId = TestEnvironment.CreateRegisteredObject<Town>();
        var defenderClanId = TestEnvironment.CreateRegisteredObject<Clan>();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(
                partyId,
                out var party));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(
                settlementId,
                out var settlement));
            Assert.True(Server.ObjectManager.TryGetObject<Town>(
                townId,
                out var town));
            Assert.True(Server.ObjectManager.TryGetObject<Clan>(
                defenderClanId,
                out var defenderClan));

            using (new AllowedThread())
            {
                settlement.Town = town;
                settlement.SetSettlementComponent(town);
                settlement.Party = new PartyBase(settlement);
                settlement.GatePosition = Position(20f, 30f);
                town.OwnerClan = defenderClan;
                town.IsOwnerUnassigned = false;
                party.ActualClan.Id = new MBGUID(1);
                defenderClan.Id = new MBGUID(2);
                party.SetMoveGoToSettlement(
                    settlement,
                    MobileParty.NavigationType.Default,
                    isTargetingThePort: false);
                party.Position = settlement.GatePosition;
                party.IsActive = true;
            }

            if (party.Party.NumberOfHealthyMembers == 0)
                party.MemberRoster.AddToCounts(party.LeaderHero.CharacterObject, 1);

            VillageHostileFactionStanceHelper.ApplyWarStance(
                party.ActualClan,
                defenderClan);

            Assert.True(settlement.IsFortification);
            Assert.Same(settlement, party.TargetSettlement);
            Assert.True(
                FactionManager.IsAtWarAgainstFaction(
                    party.MapFaction,
                    settlement.MapFaction));
            Assert.True(party.Party.NumberOfHealthyMembers > 0);
        });

        return new EntryContext(partyId, settlementId, townId);
    }

    private string RequestSettlementInteraction(
        EnvironmentInstance client,
        EntryContext context)
    {
        Server.NetworkSentMessages.Clear();
        client.NetworkSentMessages.Clear();

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(
                context.PartyId,
                out var party));
            Assert.True(client.ObjectManager.TryGetObject<Settlement>(
                context.SettlementId,
                out var settlement));
            client.Resolve<IMessageBroker>().Publish(
                this,
                new StartSettlementEncounterAttempted(party, settlement));
        }, SettlementEncounterDisabledMethods);

        var request = Assert.Single(
            client.NetworkSentMessages.GetMessages<NetworkRequestStartSettlementEncounter>());
        var approval = Assert.Single(
            Server.NetworkSentMessages.GetMessages<NetworkStartSettlementEncounter>());
        Assert.Equal(request.InteractionId, approval.InteractionId);
        Assert.Empty(
            Server.NetworkSentMessages.GetMessages<NetworkSettlementEncounterRejected>());
        return request.InteractionId;
    }

    private void SendBesiegeAttempt(
        EnvironmentInstance client,
        EntryContext context,
        IEnumerable<MethodBase> disabledMethods = null)
    {
        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(
                context.PartyId,
                out var party));
            Assert.True(client.ObjectManager.TryGetObject<Settlement>(
                context.SettlementId,
                out var settlement));
            client.Resolve<IMessageBroker>().Publish(
                this,
                new BesiegeSettlementAttempted(party, settlement));
        }, disabledMethods);
    }

    private void SendJoinAttempt(
        EnvironmentInstance client,
        EntryContext context,
        IEnumerable<MethodBase> disabledMethods = null)
    {
        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(
                context.PartyId,
                out var party));
            Assert.True(client.ObjectManager.TryGetObject<Settlement>(
                context.SettlementId,
                out var settlement));
            client.Resolve<IMessageBroker>().Publish(
                this,
                new JoinSiegeCampAttempted(party, settlement));
        }, disabledMethods);
    }

    private static CampaignVec2 Position(float x, float y) =>
        new CampaignVec2(new Vec2(x, y), isOnLand: true);

    private sealed class ThrowingPatchScope : IDisposable
    {
        private readonly Harmony harmony = new Harmony(
            $"siege-entry-validation-throwing-{Guid.NewGuid():N}");
        private readonly MethodBase method;

        public ThrowingPatchScope(MethodBase method)
        {
            this.method = method;
            harmony.Patch(
                method,
                prefix: new HarmonyMethod(
                    AccessTools.Method(
                        typeof(ThrowingPatchScope),
                        nameof(Throw))));
        }

        public void Dispose() =>
            harmony.Unpatch(method, HarmonyPatchType.Prefix, harmony.Id);

        private static void Throw() =>
            throw new InvalidOperationException("test leave failure");
    }

    private sealed record EntryContext(
        string PartyId,
        string SettlementId,
        string TownId);
}
