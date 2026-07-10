using Common;
using Common.Messaging;
using Common.Network;
using Common.Network.Coalescing;
using Coop.Core.Client.Services.MobileParties.Messages;
using System.Collections.Generic;
using Coop.Core.Server.Services.ItemRosters.Messages;
using Coop.Core.Server.Services.Stances.Messages;
using Coop.Core.Server.Services.Time.Messages;
using Common.Util;
using Coop.Core.Server.Connections.Messages;
using Coop.Core.Server.Services.MobileParties.Messages;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Services.MapEvents;
using E2E.Tests.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.Heroes.Enum;
using GameInterface.Services.Heroes.Interaces;
using GameInterface.Services.MapEventComponents.Messages;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Handlers;
using GameInterface.Services.MapEvents.Messages.Conversation;
using GameInterface.Services.MapEvents.Messages.Leave;
using GameInterface.Services.MapEvents.Messages.Start;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.MapEventSides.Messages;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Villages.Commands;
using GameInterface.Services.Villages.Data;
using GameInterface.Services.Villages.Interfaces;
using GameInterface.Services.Villages.Messages;
using HarmonyLib;
using Moq;
using System.Net;
using System.Threading;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Villages;

public class VillageHostileActionTests : MapEventTestBase
{
    private static int peerPortCounter;

    public VillageHostileActionTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void ClientRequestRaid_ForControlledVillage_StartsAndApprovesMapEvent()
    {
        var client = Clients.First();
        RegisterPeer(client, "PlayerOne");
        var (_, mobilePartyId) = CreatePlayerHeroParty("PlayerOne");
        var target = CreateVillageTarget();

        Server.NetworkSentMessages.Clear();

        RequestHostileAction(client, VillageHostileAction.Raid, mobilePartyId, target.SettlementId);

        var started = Server.NetworkSentMessages.GetMessages<NetworkVillageHostileActionStarted>().Single();
        Assert.Equal(VillageHostileAction.Raid, started.Action);
        Assert.Equal(mobilePartyId, started.MobilePartyId);
        Assert.Equal(target.SettlementId, started.SettlementId);
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkVillageHostileActionDenied>());

        var result = ConsumeApprovedMapEventStart(mobilePartyId, target.SettlementPartyId, RaidFlags());
        Assert.True(result.Approved);
        Assert.Equal(VillageHostileActionDeniedReason.Invalid, result.Reason);
    }

    [Theory]
    [InlineData(VillageHostileAction.ForceVolunteers)]
    [InlineData(VillageHostileAction.ForceSupplies)]
    public void ClientRequestForceAction_ForControlledVillage_StartsAndApprovesMapEvent(VillageHostileAction action)
    {
        var client = Clients.First();
        RegisterPeer(client, "PlayerOne");
        var (_, mobilePartyId) = CreatePlayerHeroParty("PlayerOne");
        var target = CreateVillageTarget();

        Server.NetworkSentMessages.Clear();

        RequestHostileAction(client, action, mobilePartyId, target.SettlementId);

        var started = Server.NetworkSentMessages.GetMessages<NetworkVillageHostileActionStarted>().Single();
        Assert.Equal(action, started.Action);
        Assert.Equal(mobilePartyId, started.MobilePartyId);
        Assert.Equal(target.SettlementId, started.SettlementId);
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkVillageHostileActionDenied>());

        var result = ConsumeApprovedMapEventStart(mobilePartyId, target.SettlementPartyId, HostileActionFlags(action));
        Assert.True(result.Approved);
        Assert.Equal(VillageHostileActionDeniedReason.Invalid, result.Reason);
    }

    [Fact]
    public void ClientRequestRaid_ApprovalIsSentOnlyToRequester()
    {
        var requester = Clients.First();
        var otherClient = Clients.Skip(1).First();
        RegisterPeer(requester, "PlayerOne");
        RegisterPeer(otherClient, "PlayerTwo");
        var (_, mobilePartyId) = CreatePlayerHeroParty("PlayerOne");
        var target = CreateVillageTarget();

        requester.InternalMessages.Clear();
        otherClient.InternalMessages.Clear();

        RequestHostileAction(requester, VillageHostileAction.Raid, mobilePartyId, target.SettlementId);

        var started = requester.InternalMessages.GetMessages<NetworkVillageHostileActionStarted>().Single();
        Assert.Equal(VillageHostileAction.Raid, started.Action);
        Assert.Equal(mobilePartyId, started.MobilePartyId);
        Assert.Equal(target.SettlementId, started.SettlementId);
        Assert.Empty(otherClient.InternalMessages.GetMessages<NetworkVillageHostileActionStarted>());
    }

    [Fact]
    public void ClientRequestRaid_KicksOtherPlayersOutOfVillageBeforeStarting()
    {
        var requester = Clients.First();
        var otherClient = Clients.Skip(1).First();
        RegisterPeer(requester, "PlayerOne");
        RegisterPeer(otherClient, "PlayerTwo");
        var (_, raiderMobilePartyId) = CreatePlayerHeroParty("PlayerOne");
        var (_, otherMobilePartyId) = CreatePlayerHeroParty("PlayerTwo");
        var target = CreateVillageTarget();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(raiderMobilePartyId, out var raiderParty));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(otherMobilePartyId, out var otherParty));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(target.SettlementId, out var settlement));

            EnterSettlementAction.ApplyForParty(raiderParty, settlement);
            EnterSettlementAction.ApplyForParty(otherParty, settlement);

            Assert.Same(settlement, raiderParty.CurrentSettlement);
            Assert.Same(settlement, otherParty.CurrentSettlement);
        });

        otherClient.Call(() =>
        {
            Assert.True(otherClient.ObjectManager.TryGetObject<MobileParty>(otherMobilePartyId, out var otherParty));
            Assert.True(otherClient.ObjectManager.TryGetObject<Settlement>(target.SettlementId, out var settlement));

            using (new AllowedThread())
            {
                Campaign.Current.MainParty = otherParty;
                EnterSettlementAction.ApplyForParty(otherParty, settlement);
            }

            Assert.Same(otherParty, MobileParty.MainParty);
            Assert.Same(settlement, otherParty.CurrentSettlement);
        });
        SetMockPlayerEncounter(otherClient);
        EnableHeadlessEncounterFinish(otherClient);

        requester.InternalMessages.Clear();
        otherClient.InternalMessages.Clear();
        Server.NetworkSentMessages.Clear();

        RequestHostileAction(requester, VillageHostileAction.Raid, raiderMobilePartyId, target.SettlementId);

        Assert.All(
            requester.InternalMessages.GetMessages<NetworkEndSettlementEncounter>(),
            message => Assert.Equal(otherMobilePartyId, message.PartyId));
        var endEncounter = Assert.Single(otherClient.InternalMessages.GetMessages<NetworkEndSettlementEncounter>());
        Assert.Equal(otherMobilePartyId, endEncounter.PartyId);
        Assert.All(
            Server.NetworkSentMessages.GetMessages<NetworkPartyLeaveSettlement>(),
            message => Assert.Equal(ObjectManager.Compact(otherMobilePartyId, typeof(MobileParty)), message.PartyId));
        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkVillageHostileActionStarted>());

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(raiderMobilePartyId, out var raiderParty));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(otherMobilePartyId, out var otherParty));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(target.SettlementId, out var settlement));

            Assert.Same(settlement, raiderParty.CurrentSettlement);
            Assert.Null(otherParty.CurrentSettlement);
        });
        AssertHasPlayerEncounter(otherClient, expected: false);
        otherClient.Call(() =>
        {
            Assert.True(otherClient.ObjectManager.TryGetObject<MobileParty>(otherMobilePartyId, out var otherParty));

            Assert.Null(otherParty.CurrentSettlement);
        });
    }

    [Fact]
    public void ClientRequestForceVolunteers_WithZeroHearth_IsRejected()
    {
        var client = Clients.First();
        RegisterPeer(client, "PlayerOne");
        var (_, mobilePartyId) = CreatePlayerHeroParty("PlayerOne");
        var target = CreateVillageTarget();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(target.SettlementId, out var settlement));
            using (new AllowedThread())
            {
                settlement.Village.Hearth = 0;
            }
        });

        Server.NetworkSentMessages.Clear();

        RequestHostileAction(client, VillageHostileAction.ForceVolunteers, mobilePartyId, target.SettlementId);

        var denied = Server.NetworkSentMessages.GetMessages<NetworkVillageHostileActionDenied>().Single();
        Assert.Equal(VillageHostileActionDeniedReason.HearthTooLow, denied.Reason);
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkVillageHostileActionStarted>());
    }

    [Theory]
    [InlineData(VillageHostileAction.ForceVolunteers)]
    [InlineData(VillageHostileAction.ForceSupplies)]
    public void ClientRequestForceAction_OnCooldown_IsRejected(VillageHostileAction action)
    {
        var client = Clients.First();
        RegisterPeer(client, "PlayerOne");
        var (_, mobilePartyId) = CreatePlayerHeroParty("PlayerOne");
        var target = CreateVillageTarget();
        AddServerCooldown(target.SettlementId);

        Server.NetworkSentMessages.Clear();

        RequestHostileAction(client, action, mobilePartyId, target.SettlementId);

        var denied = Server.NetworkSentMessages.GetMessages<NetworkVillageHostileActionDenied>().Single();
        Assert.Equal(VillageHostileActionDeniedReason.Cooldown, denied.Reason);
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkVillageHostileActionStarted>());
    }

    [Fact]
    public void ClientRequestRaid_ForNonVillageSettlement_IsRejected()
    {
        var client = Clients.First();
        RegisterPeer(client, "PlayerOne");
        var (_, mobilePartyId) = CreatePlayerHeroParty("PlayerOne");
        var settlementId = TestEnvironment.CreateRegisteredObject<Settlement>();

        Server.NetworkSentMessages.Clear();

        RequestHostileAction(client, VillageHostileAction.Raid, mobilePartyId, settlementId);

        var denied = Server.NetworkSentMessages.GetMessages<NetworkVillageHostileActionDenied>().Single();
        Assert.Equal(VillageHostileActionDeniedReason.NonVillageSettlement, denied.Reason);
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkVillageHostileActionStarted>());
    }

    [Fact]
    public void ClientRequestRaid_ForNonNormalVillage_IsRejected()
    {
        var client = Clients.First();
        RegisterPeer(client, "PlayerOne");
        var (_, mobilePartyId) = CreatePlayerHeroParty("PlayerOne");
        var target = CreateVillageTarget();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Village>(target.VillageId, out var village));
            using (new AllowedThread())
            {
                village.VillageState = Village.VillageStates.Looted;
            }
        });

        Server.NetworkSentMessages.Clear();

        RequestHostileAction(client, VillageHostileAction.Raid, mobilePartyId, target.SettlementId);

        var denied = Server.NetworkSentMessages.GetMessages<NetworkVillageHostileActionDenied>().Single();
        Assert.Equal(VillageHostileActionDeniedReason.InvalidVillageState, denied.Reason);
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkVillageHostileActionStarted>());
    }

    [Fact]
    public void ClientRequestRaid_WithPendingRaidApprovalForSameVillage_IsRejected()
    {
        var firstClient = Clients.First();
        var secondClient = Clients.Skip(1).First();
        RegisterPeer(firstClient, "PlayerOne");
        RegisterPeer(secondClient, "PlayerTwo");
        var (_, firstMobilePartyId) = CreatePlayerHeroParty("PlayerOne");
        var (_, secondMobilePartyId) = CreatePlayerHeroParty("PlayerTwo");
        var target = CreateVillageTarget();

        Server.NetworkSentMessages.Clear();

        RequestHostileAction(firstClient, VillageHostileAction.Raid, firstMobilePartyId, target.SettlementId);
        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkVillageHostileActionStarted>());

        Server.NetworkSentMessages.Clear();

        RequestHostileAction(secondClient, VillageHostileAction.Raid, secondMobilePartyId, target.SettlementId);

        var denied = Server.NetworkSentMessages.GetMessages<NetworkVillageHostileActionDenied>().Single();
        Assert.Equal(VillageHostileActionDeniedReason.AlreadyInMapEvent, denied.Reason);
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkVillageHostileActionStarted>());
    }

    [Fact]
    public void ClientRequestRaid_ForOwnFactionVillage_IsRejected()
    {
        var client = Clients.First();
        RegisterPeer(client, "PlayerOne");
        var (_, mobilePartyId) = CreatePlayerHeroParty("PlayerOne");
        var target = CreateVillageTarget();
        var clanId = TestEnvironment.CreateRegisteredObject<Clan>();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(mobilePartyId, out var mobileParty));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(target.SettlementId, out var settlement));
            Assert.True(Server.ObjectManager.TryGetObject<Clan>(clanId, out var clan));

            var boundSettlement = GameObjectCreator.CreateInitializedObject<Settlement>();
            var boundTown = GameObjectCreator.CreateInitializedObject<Town>();

            using (new AllowedThread())
            {
                mobileParty.ActualClan = clan;
                boundSettlement.SetSettlementComponent(boundTown);
                boundTown.OwnerClan = clan;
                settlement.Village.Bound = boundSettlement;
            }

            Assert.Equal(clan, mobileParty.MapFaction);
            Assert.Equal(clan, settlement.MapFaction);
        });

        Server.NetworkSentMessages.Clear();

        RequestHostileAction(client, VillageHostileAction.Raid, mobilePartyId, target.SettlementId);

        var denied = Server.NetworkSentMessages.GetMessages<NetworkVillageHostileActionDenied>().Single();
        Assert.Equal(VillageHostileActionDeniedReason.OwnFaction, denied.Reason);
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkVillageHostileActionStarted>());
    }

    [Fact]
    public void ForceVolunteersOutcome_AttackerVictory_AppliesRewardsAndSyncs()
    {
        var (_, mobilePartyId) = CreatePlayerHeroParty("PlayerOne");
        var target = CreateVillageTarget();
        var troopId = TestEnvironment.CreateRegisteredObject<CharacterObject>();
        var cultureId = TestEnvironment.CreateRegisteredObject<CultureObject>();

        var disabledMethods = MapEventDisabledMethods
            .Append(AccessTools.Method(typeof(SkillLevelingManager), nameof(SkillLevelingManager.OnForceVolunteers)))
            .ToList();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(mobilePartyId, out var mobileParty));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(target.SettlementId, out var settlement));
            Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(troopId, out var troop));
            Assert.True(Server.ObjectManager.TryGetObject<CultureObject>(cultureId, out var culture));

            culture.BasicTroop = troop;
            settlement.Culture = culture;
            settlement.SettlementHitPoints = 1f;
            settlement.Village.Hearth = 90f;

            var mapEvent = CreateHostileActionMapEvent(mobileParty.Party, settlement.Party, VillageHostileAction.ForceVolunteers);
            mapEvent._battleState = BattleState.AttackerVictory;

            Assert.True(Server.ObjectManager.TryGetId(mapEvent, out var _));

            Server.NetworkSentMessages.Clear();

            Server.Resolve<IVillageHostileActionInterface>().ApplyForceActionOutcome(
                mapEvent,
                VillageHostileAction.ForceVolunteers);
            Server.Resolve<IVillageHostileActionInterface>().ApplyForceActionOutcome(
                mapEvent,
                VillageHostileAction.ForceVolunteers);
        }, disabledMethods);

        AssertCooldownBroadcast(target.SettlementId);
        AssertForceVolunteersOutcome(Server, mobilePartyId, target.SettlementId, target.VillageId, troopId);
        AssertCooldownSynced(Server, target.SettlementId);
        foreach (var client in Clients)
        {
            AssertForceVolunteersOutcome(client, mobilePartyId, target.SettlementId, target.VillageId, troopId);
            AssertCooldownSynced(client, target.SettlementId);
        }
    }

    [Fact]
    public void ForceSuppliesOutcome_WithoutProductions_AppliesGoldAndCooldown()
    {
        var (_, mobilePartyId) = CreatePlayerHeroParty("PlayerOne");
        var target = CreateVillageTarget();

        var disabledMethods = MapEventDisabledMethods
            .Append(AccessTools.Method(typeof(SkillLevelingManager), nameof(SkillLevelingManager.OnForceSupplies)))
            .ToList();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(mobilePartyId, out var mobileParty));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(target.SettlementId, out var settlement));
            Assert.True(Server.ObjectManager.TryGetObject<Village>(target.VillageId, out var village));

            village.VillageType = null!;
            village.Hearth = 90f;
            settlement.SettlementHitPoints = 1f;
            mobileParty.LeaderHero.Gold = 0;

            var mapEvent = CreateHostileActionMapEvent(mobileParty.Party, settlement.Party, VillageHostileAction.ForceSupplies);
            mapEvent._battleState = BattleState.AttackerVictory;

            Assert.True(Server.ObjectManager.TryGetId(mapEvent, out var _));

            Server.NetworkSentMessages.Clear();

            Server.Resolve<IVillageHostileActionInterface>().ApplyForceActionOutcome(
                mapEvent,
                VillageHostileAction.ForceSupplies);
            Server.Resolve<IVillageHostileActionInterface>().ApplyForceActionOutcome(
                mapEvent,
                VillageHostileAction.ForceSupplies);
        }, disabledMethods);

        AssertCooldownBroadcast(target.SettlementId);
        AssertForceSuppliesGoldAndHitPointsOutcome(Server, mobilePartyId, target.SettlementId);
        AssertCooldownSynced(Server, target.SettlementId);
        foreach (var client in Clients)
        {
            AssertForceSuppliesGoldAndHitPointsOutcome(client, mobilePartyId, target.SettlementId);
            AssertCooldownSynced(client, target.SettlementId);
        }
    }

    [Fact]
    public void ClientRequestRaid_WithForgedRequesterParty_IsRejected()
    {
        var client = Clients.First();
        RegisterPeer(client, "PlayerOne");
        CreatePlayerHeroParty("PlayerOne");
        var (_, forgedMobilePartyId) = CreatePlayerHeroParty("PlayerTwo");
        var target = CreateVillageTarget();

        Server.NetworkSentMessages.Clear();

        RequestHostileAction(client, VillageHostileAction.Raid, forgedMobilePartyId, target.SettlementId);

        var denied = Server.NetworkSentMessages.GetMessages<NetworkVillageHostileActionDenied>().Single();
        Assert.Equal(VillageHostileActionDeniedReason.InvalidRequester, denied.Reason);
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkVillageHostileActionStarted>());
    }

    [Fact]
    public void ClientRequestRaid_WithInactiveRequesterParty_IsRejected()
    {
        var client = Clients.First();
        RegisterPeer(client, "PlayerOne");
        var (_, mobilePartyId) = CreatePlayerHeroParty("PlayerOne");
        var target = CreateVillageTarget();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(mobilePartyId, out var mobileParty));
            using (new AllowedThread())
            {
                mobileParty.IsActive = false;
            }
        });

        Server.NetworkSentMessages.Clear();

        RequestHostileAction(client, VillageHostileAction.Raid, mobilePartyId, target.SettlementId);

        var denied = Server.NetworkSentMessages.GetMessages<NetworkVillageHostileActionDenied>().Single();
        Assert.Equal(VillageHostileActionDeniedReason.InvalidRequester, denied.Reason);
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkVillageHostileActionStarted>());
    }

    [Fact]
    public void HostileMapEventCreation_WithoutApproval_IsRejected()
    {
        var client = Clients.First();
        var (_, mobilePartyId) = CreatePlayerHeroParty("PlayerOne");
        var target = CreateVillageTarget();
        var attackerPartyId = GetPartyBaseId(mobilePartyId);

        Server.NetworkSentMessages.Clear();

        client.Call(() => client.Resolve<INetwork>().SendAll(new NetworkRequestCreateMapEvent(
            "RaidRequest",
            attackerPartyId,
            target.SettlementPartyId,
            RaidFlags())));

        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkMapEventCreated>());
    }

    [Theory]
    [InlineData(VillageHostileAction.Raid, true, false, false)]
    [InlineData(VillageHostileAction.ForceVolunteers, false, true, false)]
    [InlineData(VillageHostileAction.ForceSupplies, false, false, true)]
    public void BeginHostileActionPresentation_RequestsAuthoritativeMapEvent(
        VillageHostileAction action,
        bool expectedForceRaid,
        bool expectedForceVolunteers,
        bool expectedForceSupplies)
    {
        var client = Clients.First();
        var (_, mobilePartyId) = CreatePlayerHeroParty("PlayerOne");
        var target = CreateVillageTarget();
        var attackerPartyId = GetPartyBaseId(mobilePartyId);
        SetMapEventCreationTimeout(client, TimeSpan.FromMilliseconds(1));

        var disabledMethods = MapEventDisabledMethods
            .Append(AccessTools.Method(typeof(GameMenu), nameof(GameMenu.SwitchToMenu), new[] { typeof(string) }))
            .Append(AccessTools.Method(
                typeof(E2E.Tests.Environment.TestNetworkRouter),
                nameof(E2E.Tests.Environment.TestNetworkRouter.SendAll),
                new[] { typeof(LiteNetLib.NetPeer), typeof(IMessage) }))
            .ToList();

        client.NetworkSentMessages.Clear();
        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(mobilePartyId, out var mobileParty));
            Assert.True(client.ObjectManager.TryGetObject<Settlement>(target.SettlementId, out var settlement));

            var encounter = ObjectHelper.SkipConstructor<PlayerEncounter>();
            encounter._attackerParty = mobileParty.Party;
            encounter._defenderParty = settlement.Party;
            Campaign.Current.PlayerEncounter = encounter;

            client.Resolve<IVillageHostileActionInterface>().BeginHostileActionPresentation(action);
        }, disabledMethods);

        var request = client.NetworkSentMessages.GetMessages<NetworkRequestCreateMapEvent>().Single();
        Assert.Equal(attackerPartyId, request.AttackerId);
        Assert.Equal(target.SettlementPartyId, request.DefenderId);
        Assert.Equal(expectedForceRaid, request.ForceRaid);
        Assert.Equal(expectedForceVolunteers, request.ForceVolunteers);
        Assert.Equal(expectedForceSupplies, request.ForceSupplies);
    }

    [Fact]
    public void HostileMapEventCreation_WithMultipleHostileFlags_IsRejectedEvenWhenApproved()
    {
        var (_, mobilePartyId) = CreatePlayerHeroParty("PlayerOne");
        var target = CreateVillageTarget();

        ApproveMapEventStart(mobilePartyId, target.SettlementId, VillageHostileAction.Raid);

        var flags = new BattleCreationFlags(
            forceRaid: true,
            forceSallyOut: false,
            forceVolunteers: true,
            forceSupplies: false,
            isSallyOutAmbush: false,
            forceBlockadeAttack: false,
            forceBlockadeSallyOutAttack: false,
            forceHideoutSendTroops: false);

        var result = ConsumeApprovedMapEventStart(mobilePartyId, target.SettlementPartyId, flags);
        Assert.False(result.Approved);
        Assert.Equal(VillageHostileActionDeniedReason.Invalid, result.Reason);

        var retry = ConsumeApprovedMapEventStart(mobilePartyId, target.SettlementPartyId, RaidFlags());
        Assert.False(retry.Approved);
        Assert.Equal(VillageHostileActionDeniedReason.NotApproved, retry.Reason);
        AssertCanStartHostileAction(mobilePartyId, target.SettlementId, VillageHostileAction.Raid);
    }

    [Theory]
    [InlineData(VillageHostileAction.Raid, Village.VillageStates.BeingRaided, typeof(RaidEventComponent))]
    [InlineData(VillageHostileAction.ForceVolunteers, Village.VillageStates.ForcedForVolunteers, typeof(ForceVolunteersEventComponent))]
    [InlineData(VillageHostileAction.ForceSupplies, Village.VillageStates.ForcedForSupplies, typeof(ForceSuppliesEventComponent))]
    public void ServerCreatesHostileActionMapEvent_ClientsReceiveComponentAndVillageState(
        VillageHostileAction action,
        Village.VillageStates expectedVillageState,
        Type expectedComponentType)
    {
        var (_, mobilePartyId) = CreatePlayerHeroParty("PlayerOne");
        var target = CreateVillageTarget();
        string? mapEventId = null;
        string? componentId = null;

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(mobilePartyId, out var mobileParty));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(target.SettlementId, out var settlement));

            var mapEvent = CreateHostileActionMapEvent(mobileParty.Party, settlement.Party, action);

            Assert.NotNull(mapEvent);
            Assert.NotNull(mapEvent.Component);
            Assert.IsType(expectedComponentType, mapEvent.Component);
            Assert.True(Server.ObjectManager.TryGetId(mapEvent, out mapEventId));
            Assert.True(Server.ObjectManager.TryGetId(mapEvent.Component, out componentId));
        }, MapEventDisabledMethods);

        Assert.NotNull(mapEventId);
        Assert.NotNull(componentId);
        AssertHostileActionMapEvent(Server, mapEventId!, componentId!, expectedComponentType, target.VillageId, expectedVillageState);
        foreach (var client in Clients)
        {
            AssertHostileActionMapEvent(client, mapEventId!, componentId!, expectedComponentType, target.VillageId, expectedVillageState);
        }
    }

    [Fact]
    public void RaidEventUpdate_LootingPhase_AppliesProgressAndSyncs()
    {
        var (heroId, mobilePartyId) = CreatePlayerHeroParty("PlayerOne");
        var target = CreateVillageTarget();
        var nativeItemStringId = "grain";
        string? itemId = null;
        RemoveNativeItemObjectFromObjectManagers(nativeItemStringId);
        var villageTypeId = TestEnvironment.CreateRegisteredObject<VillageType>();
        string? mapEventId = null;
        string? componentId = null;

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(mobilePartyId, out var mobileParty));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(target.SettlementId, out var settlement));
            Assert.True(Server.ObjectManager.TryGetObject<Village>(target.VillageId, out var village));
            var item = GetNativeItemObject(nativeItemStringId);
            Assert.NotNull(item);
            Assert.False(Server.ObjectManager.TryGetId(item, out _));
            Assert.True(Server.ObjectManager.TryGetObject<VillageType>(villageTypeId, out var villageType));

            using (new AllowedThread())
            {
                mobileParty.MemberRoster.AddToCounts(hero.CharacterObject, 100);
                hero.PartyBelongedTo = mobileParty;
                hero.Gold = 0;
                settlement.ItemRoster.AddToCounts(new EquipmentElement(item), 3);
                villageType._productions = new MBList<(ItemObject, float)>
                {
                    (item, 120f),
                };
                village.VillageType = villageType;
                settlement.SettlementHitPoints = 1f;
                settlement.Village.Hearth = 100f;
                Campaign.Current.MapTimeTracker._deltaTimeInTicks = CampaignTime.Hours(10f).NumTicks;
            }

            var mapEvent = CreateHostileActionMapEvent(mobileParty.Party, settlement.Party, VillageHostileAction.Raid);
            Assert.NotNull(mapEvent);
            Assert.IsType<RaidEventComponent>(mapEvent.Component);
            Assert.True(Server.ObjectManager.TryGetId(mapEvent, out mapEventId));
            Assert.True(Server.ObjectManager.TryGetId(mapEvent.Component, out componentId));

            var component = (RaidEventComponent)mapEvent.Component;
            var wasFinished = false;
            for (int i = 0; i < 25 && component.RaidDamage <= 0; i++)
            {
                component.Update(ref wasFinished);
            }

            component._raidProductionRewards[item] = 1f;
            MessageBroker.Instance.Publish(component, new RaidProductionRewardsUpdated(component));
            Assert.True(Server.ObjectManager.TryGetId(item, out itemId));

            Assert.True(mapEvent.WasEverInLootingPhase);
            Assert.True(component.RaidDamage > 0);
            Assert.True(settlement.SettlementHitPoints < 1f);
            Assert.True(settlement.Village.Hearth < 100f);
            Assert.True(GetItemAmount(component._raidProductionRewards, item) > 0);
        }, MapEventDisabledMethods);

        Assert.NotNull(mapEventId);
        Assert.NotNull(componentId);
        Assert.NotNull(itemId);
        AssertRaidProgressOutcome(Server, mapEventId!, componentId!, mobilePartyId, target.SettlementId, target.VillageId, itemId);
        foreach (var client in Clients)
        {
            AssertRaidProgressOutcome(client, mapEventId!, componentId!, mobilePartyId, target.SettlementId, target.VillageId, itemId);
        }
    }

    [Fact]
    public void RaidLootedItemsUpdated_ClientRaisesItemsLootedForVanillaPlunderUi()
    {
        var client = Clients.First();
        var (_, mobilePartyId) = CreatePlayerHeroParty("PlayerOne");
        var itemId = TestEnvironment.CreateRegisteredObject<ItemObject>();
        var listenerOwner = new object();
        var capturedAmount = 0;

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(mobilePartyId, out var mobileParty));
            Assert.True(client.ObjectManager.TryGetObject<ItemObject>(itemId, out var item));

            CampaignEvents.ItemsLooted.AddNonSerializedListener(listenerOwner, (MobileParty party, ItemRoster lootedItems) =>
            {
                if (party != mobileParty)
                    return;

                capturedAmount = GetItemAmount(lootedItems, item);
            });
        });

        Server.NetworkSentMessages.Clear();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(mobilePartyId, out var mobileParty));
            Assert.True(Server.ObjectManager.TryGetObject<ItemObject>(itemId, out var item));

            var lootedItems = new ItemRoster();
            lootedItems.AddToCounts(new EquipmentElement(item), 2);
            MessageBroker.Instance.Publish(this, new RaidLootedItemsUpdated(mobileParty, lootedItems));
        });

        client.Call(() => CampaignEvents.ItemsLooted.ClearListeners(listenerOwner));

        var message = Server.NetworkSentMessages.GetMessages<NetworkRaidLootedItemsUpdated>().Single();
        Assert.Equal(mobilePartyId, message.PartyId);
        Assert.Contains(itemId, message.ItemIds);
        Assert.Contains(2, message.Amounts);
        Assert.Equal(2, capturedAmount);
    }

    [Theory]
    [InlineData(0f, Village.VillageStates.Looted)]
    [InlineData(0.5f, Village.VillageStates.Normal)]
    public void RaidFinalization_SyncsVillageStateAndDestroysMapEvent(
        float settlementHitPoints,
        Village.VillageStates expectedVillageState)
    {
        var (heroId, mobilePartyId) = CreatePlayerHeroParty("PlayerOne");
        var target = CreateVillageTarget();
        string? mapEventId = null;

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(mobilePartyId, out var mobileParty));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(target.SettlementId, out var settlement));

            using (new AllowedThread())
            {
                mobileParty.MemberRoster.AddToCounts(hero.CharacterObject, 1);
                hero.PartyBelongedTo = mobileParty;
            }

            var mapEvent = CreateHostileActionMapEvent(mobileParty.Party, settlement.Party, VillageHostileAction.Raid);
            Assert.NotNull(mapEvent);
            mapEvent.MapEventVisual = MockMapEventVisual();
            settlement.SettlementHitPoints = settlementHitPoints;
            mapEvent.BattleState = expectedVillageState == Village.VillageStates.Looted
                ? BattleState.AttackerVictory
                : BattleState.DefenderVictory;

            Assert.True(Server.ObjectManager.TryGetId(mapEvent, out mapEventId));
            mapEvent.FinalizeEvent();
        }, MapEventDisabledMethods);

        Assert.NotNull(mapEventId);
        AssertRaidFinalizedOutcome(Server, mapEventId!, target.SettlementId, target.VillageId, expectedVillageState, settlementHitPoints);
        foreach (var client in Clients)
        {
            AssertRaidFinalizedOutcome(client, mapEventId!, target.SettlementId, target.VillageId, expectedVillageState, settlementHitPoints);
        }
    }

    [Fact]
    public void RaidDefenderVictoryFinalizeRequest_ResetsPlayersToVillage()
    {
        var client = Clients.First();
        var (heroId, mobilePartyId) = CreatePlayerHeroParty("PlayerOne");
        var attackerPartyId = GetPartyBaseId(mobilePartyId);
        var defenderTroopId = TestEnvironment.CreateRegisteredObject<CharacterObject>();
        var target = CreateVillageTarget();
        string? mapEventId = null;

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(mobilePartyId, out var mobileParty));
            Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(defenderTroopId, out var defenderTroop));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(target.SettlementId, out var settlement));

            using (new AllowedThread())
            {
                mobileParty.MemberRoster.AddToCounts(hero.CharacterObject, 1);
                hero.PartyBelongedTo = mobileParty;
                settlement.Party.MemberRoster.AddToCounts(defenderTroop, 1);
                settlement.SettlementHitPoints = 0.5f;
            }

            var mapEvent = CreateHostileActionMapEvent(mobileParty.Party, settlement.Party, VillageHostileAction.Raid);
            mapEvent._battleState = BattleState.AttackerVictory;

            Assert.True(mapEvent.HasWinner);
            Assert.True(mapEvent.DefenderSide.TroopCount > 0);
            Assert.True(Server.ObjectManager.TryGetId(mapEvent, out mapEventId));
        }, MapEventDisabledMethods);

        Assert.NotNull(mapEventId);
        Server.NetworkSentMessages.Clear();

        client.Call(() => client.Resolve<INetwork>().SendAll(new NetworkMapEventFinalizeAttempted(mapEventId!)), MapEventDisabledMethods);

        var reset = Server.NetworkSentMessages.GetMessages<NetworkRaidBattleResetToVillage>().Single();
        Assert.Equal(target.SettlementId, reset.SettlementId);
        Assert.Contains(attackerPartyId, reset.PartyIds);
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkMapEventFinalized>());
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkChangeBattleState>());

        Server.Call(() =>
        {
            Assert.False(Server.ObjectManager.TryGetObject<MapEvent>(mapEventId!, out var _));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(mobilePartyId, out var mobileParty));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(target.SettlementId, out var settlement));
            Assert.True(Server.ObjectManager.TryGetObject<Village>(target.VillageId, out var village));

            Assert.Null(mobileParty.Party.MapEventSide);
            Assert.Same(settlement, mobileParty.CurrentSettlement);
            Assert.Equal(Village.VillageStates.Normal, village.VillageState);
            Assert.Equal(1f, settlement.SettlementHitPoints, 3);
        }, MapEventDisabledMethods);
    }

    [Fact]
    public void RaidFinalizeRequest_EndingSlowRaidMovesRaiderToVillageGate()
    {
        var client = Clients.First();
        var (_, mobilePartyId) = CreatePlayerHeroParty("PlayerOne");
        var target = CreateVillageTarget();
        var gatePosition = new CampaignVec2(new Vec2(42f, 24f), true);
        var insidePosition = new CampaignVec2(new Vec2(40f, 24f), true);
        string? mapEventId = null;

        void PlaceRaiderInsideVillage(EnvironmentInstance instance)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(mobilePartyId, out var mobileParty));
                Assert.True(instance.ObjectManager.TryGetObject<Settlement>(target.SettlementId, out var settlement));

                using (new AllowedThread())
                {
                    settlement.GatePosition = gatePosition;
                    mobileParty.Position = insidePosition;
                    mobileParty.CurrentSettlement = settlement;
                    mobileParty.ResetNavigationToHold();
                }
            });
        }

        PlaceRaiderInsideVillage(Server);
        foreach (var syncedClient in Clients)
        {
            PlaceRaiderInsideVillage(syncedClient);
        }

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(mobilePartyId, out var mobileParty));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(target.SettlementId, out var settlement));

            var mapEvent = CreateHostileActionMapEvent(mobileParty.Party, settlement.Party, VillageHostileAction.Raid);
            Assert.True(mapEvent.IsActiveSlowVillageRaid());
            Assert.True(Server.ObjectManager.TryGetId(mapEvent, out mapEventId));
        }, MapEventDisabledMethods);

        Assert.NotNull(mapEventId);
        EnableHeadlessEncounterFinish(client);
        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(mobilePartyId, out var mobileParty));
            Assert.True(client.ObjectManager.TryGetObject<MapEvent>(mapEventId!, out var mapEvent));

            using (new AllowedThread())
            {
                Campaign.Current.MainParty = mobileParty;
            }

            var encounter = ObjectHelper.SkipConstructor<PlayerEncounter>();
            encounter._mapEvent = mapEvent;
            encounter.ForceRaid = true;
            Campaign.Current.PlayerEncounter = encounter;
        }, MapEventDisabledMethods);

        Server.NetworkSentMessages.Clear();
        var disabledMethods = MapEventDisabledMethods
            .Append(AccessTools.Method(typeof(GameMenu), nameof(GameMenu.ExitToLast)))
            .ToList();

        client.Call(() => client.Resolve<INetwork>().SendAll(new NetworkMapEventFinalizeAttempted(mapEventId!)), disabledMethods);
        Server.Call(() => Server.Resolve<ISendCoalescer>().Flush(Server.Resolve<INetwork>()));

        AssertRaidPartyMovedToVillageGate(Server, mobilePartyId, target.SettlementId);
        foreach (var syncedClient in Clients)
        {
            AssertRaidPartyMovedToVillageGate(syncedClient, mobilePartyId, target.SettlementId);
        }
    }

    [Fact]
    public void RaidFinalizeRequest_ForAlreadyDestroyedMapEvent_StillClosesRequesterMenu()
    {
        var client = Clients.First();
        Server.NetworkSentMessages.Clear();

        client.Call(() => client.Resolve<INetwork>().SendAll(new NetworkMapEventFinalizeAttempted("already-finalized-raid")));

        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkMapEventFinalized>());
    }

    [Fact]
    public void VillageRecovery_DailyTickSettlement_HealsLootedVillageAndSyncs()
    {
        var target = CreateVillageTarget();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(target.SettlementId, out var settlement));

            settlement.Village.VillageState = Village.VillageStates.Looted;
            settlement.SettlementHitPoints = 0.99f;
        });

        AssertVillageStateAndHitPoints(Server, target.SettlementId, target.VillageId, Village.VillageStates.Looted, 0.99f);
        foreach (var client in Clients)
        {
            AssertVillageStateAndHitPoints(client, target.SettlementId, target.VillageId, Village.VillageStates.Looted, 0.99f);
        }

        var disabledMethods = MapEventDisabledMethods
            .Append(AccessTools.Method(typeof(Settlement), "TransferReadyMilitiasToMilitiaParty"))
            .Append(AccessTools.Method(typeof(Settlement), "AddMilitiasToParty"))
            .Where(method => method != null)
            .ToList();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(target.SettlementId, out var settlement));

            InvokeVillageHealDailyTick(settlement);
        }, disabledMethods);

        AssertVillageStateAndHitPoints(Server, target.SettlementId, target.VillageId, Village.VillageStates.Normal, 1f);
        foreach (var client in Clients)
        {
            AssertVillageStateAndHitPoints(client, target.SettlementId, target.VillageId, Village.VillageStates.Normal, 1f);
        }
    }

    [Fact]
    public void PlayerEnteringCampaign_ReceivesActiveForceActionCooldownSnapshot()
    {
        var client = Clients.First();
        var target = CreateVillageTarget();
        AddServerCooldown(target.SettlementId);

        Server.NetworkSentMessages.Clear();

        Server.SimulateMessage(this, new PlayerCampaignEntered(client.NetPeer));

        var cooldowns = Server.NetworkSentMessages.GetMessages<NetworkVillageHostileActionCooldowns>().Single();
        Assert.Contains(cooldowns.Cooldowns, c => c.SettlementId == target.SettlementId);
        AssertCooldownSynced(client, target.SettlementId);
    }

    [Fact]
    public void PlayerEnteringCampaign_ReceivesRaidAiInterventionConfigSnapshot()
    {
        var client = Clients.First();
        var previous = MapEventConfig.AllowRaidAiIntervention;

        try
        {
            Server.Call(() => MapEventConfig.AllowRaidAiIntervention = false);

            Server.NetworkSentMessages.Clear();

            Server.SimulateMessage(this, new PlayerCampaignEntered(client.NetPeer));

            var update = Server.NetworkSentMessages.GetMessages<NetworkRaidAiInterventionConfigChanged>().Single();
            Assert.False(update.Allow);
            client.Call(() => Assert.False(MapEventConfig.AllowRaidAiIntervention));
        }
        finally
        {
            Server.Call(() => MapEventConfig.AllowRaidAiIntervention = previous);
            foreach (var syncedClient in Clients)
            {
                syncedClient.Call(() => MapEventConfig.AllowRaidAiIntervention = previous);
            }
        }
    }

    [Fact]
    public void SlowRaidMapEvent_WithPlayerParty_ServerUpdateIsAllowed()
    {
        var (_, mobilePartyId) = CreatePlayerHeroParty("PlayerOne");
        var target = CreateVillageTarget();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(mobilePartyId, out var mobileParty));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(target.SettlementId, out var settlement));

            var mapEvent = GameObjectCreator.CreateInitializedObject<MapEvent>();
            mapEvent.MapEventVisual = MockMapEventVisual();
            mapEvent.Initialize(
                mobileParty.Party,
                settlement.Party,
                new RaidEventComponent(mapEvent),
                MapEvent.BattleTypes.Raid);

            Assert.True(mapEvent.IsActiveSlowVillageRaid());
            Assert.True(InvokeMapEventUpdatePrefix(mapEvent));
        }, MapEventDisabledMethods);
    }

    [Fact]
    public void SlowRaidMapEvent_WithDefenderTroopsAfterLootingPhase_ServerUpdateIsBlocked()
    {
        var (_, mobilePartyId) = CreatePlayerHeroParty("PlayerOne");
        var defenderTroopId = TestEnvironment.CreateRegisteredObject<CharacterObject>();
        var target = CreateVillageTarget();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(mobilePartyId, out var mobileParty));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(target.SettlementId, out var settlement));
            Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(defenderTroopId, out var defenderTroop));

            var mapEvent = CreateHostileActionMapEvent(mobileParty.Party, settlement.Party, VillageHostileAction.Raid);
            mapEvent.WasEverInLootingPhase = true;

            var defenderParty = GameObjectCreator.CreateInitializedObject<MobileParty>();
            defenderParty.MemberRoster.AddToCounts(defenderTroop, 1);
            AddSyntheticMapEventParty(mapEvent.DefenderSide, defenderParty.Party);

            Assert.False(mapEvent.IsActiveSlowVillageRaid());
            Assert.False(InvokeMapEventUpdatePrefix(mapEvent));
        }, MapEventDisabledMethods);
    }

    [Fact]
    public void SlowRaidMapEvent_WithDefenderTroopsAndAiInterventionDisabled_ServerUpdateIsAllowed()
    {
        var previous = MapEventConfig.AllowRaidAiIntervention;
        MapEventConfig.AllowRaidAiIntervention = false;
        try
        {
            var (heroId, mobilePartyId) = CreatePlayerHeroParty("PlayerOne");
            var defenderTroopId = TestEnvironment.CreateRegisteredObject<CharacterObject>();
            var raidItemId = TestEnvironment.CreateRegisteredObject<ItemObject>();
            var villageTypeId = TestEnvironment.CreateRegisteredObject<VillageType>();
            var target = CreateVillageTarget();

            Server.Call(() =>
            {
                Assert.True(Server.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
                Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(mobilePartyId, out var mobileParty));
                Assert.True(Server.ObjectManager.TryGetObject<Settlement>(target.SettlementId, out var settlement));
                Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(defenderTroopId, out var defenderTroop));
                Assert.True(Server.ObjectManager.TryGetObject<ItemObject>(raidItemId, out var raidItem));
                Assert.True(Server.ObjectManager.TryGetObject<VillageType>(villageTypeId, out var villageType));

                using (new AllowedThread())
                {
                    mobileParty.MemberRoster.AddToCounts(hero.CharacterObject, 100);
                    hero.PartyBelongedTo = mobileParty;
                    settlement.ItemRoster.AddToCounts(new EquipmentElement(raidItem), 3);
                    villageType._productions = new MBList<(ItemObject, float)>
                    {
                        (raidItem, 120f),
                    };
                    settlement.Village.VillageType = villageType;
                    settlement.SettlementHitPoints = 1f;
                    settlement.Village.Hearth = 100f;
                    Campaign.Current.MapTimeTracker._deltaTimeInTicks = CampaignTime.Hours(10f).NumTicks;
                }

                var mapEvent = CreateHostileActionMapEvent(mobileParty.Party, settlement.Party, VillageHostileAction.Raid);
                var component = Assert.IsType<RaidEventComponent>(mapEvent.Component);

                var defenderParty = GameObjectCreator.CreateInitializedObject<MobileParty>();
                defenderParty.MemberRoster.AddToCounts(defenderTroop, 1);
                AddSyntheticMapEventParty(mapEvent.DefenderSide, defenderParty.Party);

                Assert.True(mapEvent.IsActiveSlowVillageRaid());
                Assert.True(mapEvent.DefenderSide.TroopCount > 0);
                Assert.True(InvokeMapEventUpdatePrefix(mapEvent));

                Assert.Null(defenderParty.Party.MapEventSide);
                Assert.DoesNotContain(mapEvent.DefenderSide.Parties, party => party.Party == defenderParty.Party);
                Assert.Equal(0, mapEvent.DefenderSide.TroopCount);

                var wasFinished = false;
                for (int i = 0; i < 25 && component.RaidDamage <= 0; i++)
                {
                    component.Update(ref wasFinished);
                }

                Assert.True(component.RaidDamage > 0);
            }, MapEventDisabledMethods);
        }
        finally
        {
            MapEventConfig.AllowRaidAiIntervention = previous;
        }
    }

    [Fact]
    public void SlowRaidMapEvent_WithAiInterventionEnabled_AllowsAiDefenderJoin()
    {
        var previous = MapEventConfig.AllowRaidAiIntervention;
        MapEventConfig.AllowRaidAiIntervention = true;
        try
        {
            var (_, mobilePartyId) = CreatePlayerHeroParty("PlayerOne");
            var defenderTroopId = TestEnvironment.CreateRegisteredObject<CharacterObject>();
            var target = CreateVillageTarget();

            Server.Call(() =>
            {
                Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(mobilePartyId, out var mobileParty));
                Assert.True(Server.ObjectManager.TryGetObject<Settlement>(target.SettlementId, out var settlement));
                Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(defenderTroopId, out var defenderTroop));

                var mapEvent = CreateHostileActionMapEvent(mobileParty.Party, settlement.Party, VillageHostileAction.Raid);

                var defenderParty = GameObjectCreator.CreateInitializedObject<MobileParty>();
                defenderParty.MemberRoster.AddToCounts(defenderTroop, 1);

                Assert.True(mapEvent.IsActiveSlowVillageRaid());
                Assert.True(InvokeCanPartyJoinBattlePostfix(mapEvent, defenderParty.Party, initialResult: true));

                defenderParty.Party.MapEventSide = mapEvent.DefenderSide;

                Assert.Same(mapEvent.DefenderSide, defenderParty.Party.MapEventSide);
                Assert.Contains(mapEvent.DefenderSide.Parties, party => party.Party == defenderParty.Party);
                Assert.False(mapEvent.IsActiveSlowVillageRaid());
            }, MapEventDisabledMethods);
        }
        finally
        {
            MapEventConfig.AllowRaidAiIntervention = previous;
        }
    }

    [Fact]
    public void SlowRaidMapEvent_WithAiInterventionDisabled_BlocksAiDefenderJoin()
    {
        var previous = MapEventConfig.AllowRaidAiIntervention;
        MapEventConfig.AllowRaidAiIntervention = false;
        try
        {
            var (_, mobilePartyId) = CreatePlayerHeroParty("PlayerOne");
            var defenderTroopId = TestEnvironment.CreateRegisteredObject<CharacterObject>();
            var target = CreateVillageTarget();

            Server.Call(() =>
            {
                Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(mobilePartyId, out var mobileParty));
                Assert.True(Server.ObjectManager.TryGetObject<Settlement>(target.SettlementId, out var settlement));
                Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(defenderTroopId, out var defenderTroop));

                var mapEvent = CreateHostileActionMapEvent(mobileParty.Party, settlement.Party, VillageHostileAction.Raid);

                var defenderParty = GameObjectCreator.CreateInitializedObject<MobileParty>();
                defenderParty.MemberRoster.AddToCounts(defenderTroop, 1);
                defenderParty.Party.MapEventSide = mapEvent.DefenderSide;

                Assert.Null(defenderParty.Party.MapEventSide);
                Assert.DoesNotContain(mapEvent.DefenderSide.Parties, party => party.Party == defenderParty.Party);
                Assert.True(mapEvent.IsActiveSlowVillageRaid());
                Assert.True(InvokeMapEventUpdatePrefix(mapEvent));
            }, MapEventDisabledMethods);
        }
        finally
        {
            MapEventConfig.AllowRaidAiIntervention = previous;
        }
    }

    [Fact]
    public void SlowRaidMapEvent_WithAiInterventionDisabled_BlocksAiEncounterStarts()
    {
        var previous = MapEventConfig.AllowRaidAiIntervention;
        MapEventConfig.AllowRaidAiIntervention = false;
        try
        {
            var (_, mobilePartyId) = CreatePlayerHeroParty("PlayerOne");
            var defenderTroopId = TestEnvironment.CreateRegisteredObject<CharacterObject>();
            var target = CreateVillageTarget();

            Server.Call(() =>
            {
                Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(mobilePartyId, out var mobileParty));
                Assert.True(Server.ObjectManager.TryGetObject<Settlement>(target.SettlementId, out var settlement));
                Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(defenderTroopId, out var defenderTroop));

                var mapEvent = CreateHostileActionMapEvent(mobileParty.Party, settlement.Party, VillageHostileAction.Raid);
                AddSyntheticMapEventParty(mapEvent.DefenderSide, settlement.Party);

                var defenderParty = GameObjectCreator.CreateInitializedObject<MobileParty>();
                defenderParty.MemberRoster.AddToCounts(defenderTroop, 1);

                Assert.True(mapEvent.IsActiveSlowVillageRaid());
                Assert.True(settlement.Party.MapEvent.IsRaidAiInterventionSuppressed());
                Assert.False(InvokeStartSettlementEncounterPrefix(defenderParty, settlement));
                Assert.False(InvokeStartPartyEncounterPrefix(defenderParty.Party, mobileParty.Party));
                Assert.False(InvokeRestartPlayerEncounterPrefix(defenderParty.Party, mobileParty.Party));
                Assert.True(InvokeMapEventUpdatePrefix(mapEvent));
                Assert.Null(defenderParty.Party.MapEventSide);
                Assert.Null(defenderParty.Party.MapEvent);
            }, MapEventDisabledMethods);
        }
        finally
        {
            MapEventConfig.AllowRaidAiIntervention = previous;
        }
    }

    [Fact]
    public void RaidAiInterventionDebugCommand_ClientRequestUpdatesServerAndClients()
    {
        var client = Clients.First();
        var previous = MapEventConfig.AllowRaidAiIntervention;
        MapEventConfig.AllowRaidAiIntervention = true;
        try
        {
            Server.NetworkSentMessages.Clear();
            client.NetworkSentMessages.Clear();

            client.Call(() =>
            {
                var result = RaidDebugCommands.AllowRaidAiIntervention(new List<string> { "off" });
                Assert.Contains("server update requested", result);
            });

            var request = client.NetworkSentMessages.GetMessages<NetworkRequestRaidAiInterventionConfigChange>().Single();
            Assert.False(request.Allow);

            var update = Server.NetworkSentMessages.GetMessages<NetworkRaidAiInterventionConfigChanged>().Single();
            Assert.False(update.Allow);

            Server.Call(() => Assert.False(MapEventConfig.AllowRaidAiIntervention));
            foreach (var syncedClient in Clients)
            {
                syncedClient.Call(() => Assert.False(MapEventConfig.AllowRaidAiIntervention));
            }
        }
        finally
        {
            MapEventConfig.AllowRaidAiIntervention = previous;
        }
    }

    [Fact]
    public void SlowRaidMapEvent_DoesNotBlockFastForward()
    {
        var (_, mobilePartyId) = CreatePlayerHeroParty("PlayerOne");
        var target = CreateVillageTarget();

        Server.Call(() => Server.Resolve<ITimeControlInterface>().ServerSetTimeControl(TimeControlEnum.Play_2x));
        Server.NetworkSentMessages.Clear();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(mobilePartyId, out var mobileParty));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(target.SettlementId, out var settlement));

            var mapEvent = GameObjectCreator.CreateInitializedObject<MapEvent>();
            mapEvent.MapEventVisual = MockMapEventVisual();
            mapEvent.Initialize(
                mobileParty.Party,
                settlement.Party,
                new RaidEventComponent(mapEvent),
                MapEvent.BattleTypes.Raid);

            Assert.True(mapEvent.IsUnopposedVillageRaid());
            Assert.Equal(TimeControlEnum.Play_2x, Server.Resolve<ITimeControlInterface>().GetTimeControl());
        }, MapEventDisabledMethods);

        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkMapEventLockChanged>());
    }

    [Fact]
    public void ActiveSlowRaidMapEvent_SecondPlayerConversationIsAllowed()
    {
        var client = Clients.First();
        var (_, raiderMobilePartyId) = CreatePlayerHeroParty("PlayerOne");
        var (_, joinerMobilePartyId) = CreatePlayerHeroParty("PlayerTwo");
        var target = CreateVillageTarget();
        var raiderPartyId = GetPartyBaseId(raiderMobilePartyId);
        var joinerPartyId = GetPartyBaseId(joinerMobilePartyId);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(raiderMobilePartyId, out var raiderParty));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(target.SettlementId, out var settlement));

            var mapEvent = CreateHostileActionMapEvent(raiderParty.Party, settlement.Party, VillageHostileAction.Raid);

            Assert.True(mapEvent.IsActiveSlowVillageRaid());
        }, MapEventDisabledMethods);

        Server.NetworkSentMessages.Clear();

        RequestConversation(client, joinerPartyId, raiderPartyId);

        var allowed = Server.NetworkSentMessages.GetMessages<NetworkAllowConversation>().Single();
        Assert.Equal(raiderPartyId, allowed.DefenderId);
        Assert.Equal(joinerPartyId, allowed.AttackerId);
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkConversationDenied>());
    }

    [Fact]
    public void ActiveSlowRaidMapEvent_StartSettlementEncounterRequestsJoinConversation()
    {
        var client = Clients.First();
        client.Resolve<IControllerIdProvider>().SetControllerId("PlayerTwo");
        var (_, raiderMobilePartyId) = CreatePlayerHeroParty("PlayerOne");
        var (_, joinerMobilePartyId) = CreatePlayerHeroParty("PlayerTwo");
        var target = CreateVillageTarget();
        var joinerPartyId = GetPartyBaseId(joinerMobilePartyId);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(raiderMobilePartyId, out var raiderParty));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(target.SettlementId, out var settlement));

            var mapEvent = CreateHostileActionMapEvent(raiderParty.Party, settlement.Party, VillageHostileAction.Raid);

            Assert.True(mapEvent.IsActiveSlowVillageRaid());
        }, MapEventDisabledMethods);

        client.NetworkSentMessages.Clear();

        client.Call(() =>
        {
            client.Resolve<IControllerIdProvider>().SetControllerId("PlayerTwo");
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(joinerMobilePartyId, out var joinerParty));
            Assert.True(client.ObjectManager.TryGetObject<Settlement>(target.SettlementId, out var settlement));
            Assert.True(client.ObjectManager.TryGetObject<Village>(target.VillageId, out var village));
            if (!client.ObjectManager.TryGetObject<PartyBase>(target.SettlementPartyId, out var settlementParty))
            {
                settlementParty = new PartyBase(settlement);
                Assert.True(client.ObjectManager.AddExisting(target.SettlementPartyId, settlementParty));
            }

            using (new AllowedThread())
            {
                settlement.Village = village;
                settlement.SetSettlementComponent(village);
                settlement.Party = settlementParty;
            }

            Assert.True(joinerParty.IsControlledByThisInstance());
            Assert.True(village.VillageState == Village.VillageStates.BeingRaided || settlement.Party.MapEvent?.IsActiveSlowVillageRaid() == true);
            var patchType = AccessTools.TypeByName("GameInterface.Services.MapEvents.Patches.EncounterManagerPatches");
            var prefix = AccessTools.Method(patchType, "Prefix");
            Assert.NotNull(prefix);

            var runOriginal = (bool)prefix.Invoke(null, new object[] { joinerParty, settlement })!;

            Assert.False(runOriginal);
        });

        var request = client.NetworkSentMessages.GetMessages<NetworkRequestConversation>().Single();
        Assert.Equal(target.SettlementPartyId, request.DefenderId);
        Assert.Equal(joinerPartyId, request.AttackerId);
        Assert.Equal(ConversationRestartSource.EncounterManager, request.Source);
        Assert.Empty(client.NetworkSentMessages.GetMessages<NetworkRequestStartSettlementEncounter>());
    }

    [Fact]
    public void RaidDefenderJoin_WithResistancePhase_UsesNormalBattleJoinReplication()
    {
        var client = Clients.First();
        var (raiderHeroId, raiderMobilePartyId) = CreatePlayerHeroParty("PlayerOne");
        var (joinerHeroId, joinerMobilePartyId) = CreatePlayerHeroParty("PlayerTwo");
        var defenderTroopId = TestEnvironment.CreateRegisteredObject<CharacterObject>();
        var target = CreateVillageTarget();
        var joinerPartyId = GetPartyBaseId(joinerMobilePartyId);
        string? raidMapEventId = null;

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(raiderHeroId, out var raiderHero));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(joinerHeroId, out var joinerHero));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(raiderMobilePartyId, out var raiderParty));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(joinerMobilePartyId, out var joinerParty));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(target.SettlementId, out var settlement));
            Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(defenderTroopId, out var defenderTroop));

            using (new AllowedThread())
            {
                raiderParty.MemberRoster.AddToCounts(raiderHero.CharacterObject, 1);
                joinerParty.MemberRoster.AddToCounts(joinerHero.CharacterObject, 1);
                raiderHero.PartyBelongedTo = raiderParty;
                joinerHero.PartyBelongedTo = joinerParty;
            }

            var raidMapEvent = CreateHostileActionMapEvent(raiderParty.Party, settlement.Party, VillageHostileAction.Raid);
            Assert.NotNull(raidMapEvent);
            raidMapEvent.MapEventVisual = MockMapEventVisual();

            var defenderParty = GameObjectCreator.CreateInitializedObject<MobileParty>();
            defenderParty.MemberRoster.AddToCounts(defenderTroop, 1);
            AddSyntheticMapEventParty(raidMapEvent.DefenderSide, defenderParty.Party);

            Assert.False(raidMapEvent.IsActiveSlowVillageRaid());
            Assert.True(Server.ObjectManager.TryGetId(raidMapEvent, out raidMapEventId));
        }, MapEventDisabledMethods);

        Assert.NotNull(raidMapEventId);

        client.Call(() => client.Resolve<INetwork>().SendAll(new NetworkRequestJoinBattle(
            raidMapEventId!,
            joinerPartyId,
            BattleSideEnum.Defender)), MapEventDisabledMethods);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(raidMapEventId!, out var mapEvent));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(raiderMobilePartyId, out var raiderParty));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(joinerMobilePartyId, out var joinerParty));
            Assert.Same(mapEvent, joinerParty.MapEvent);
            Assert.Same(mapEvent, raiderParty.MapEvent);
            Assert.True(mapEvent.IsRaid);
            Assert.IsType<RaidEventComponent>(mapEvent.Component);
            Assert.Same(joinerParty.Party.MapEventSide, mapEvent.DefenderSide);
            Assert.Same(raiderParty.Party.MapEventSide, mapEvent.AttackerSide);
        }, MapEventDisabledMethods);

        foreach (var instance in Clients)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<MapEvent>(raidMapEventId!, out var mapEvent));
                Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(joinerMobilePartyId, out var joinerParty));
                Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(raiderMobilePartyId, out var raiderParty));
                Assert.Same(mapEvent, joinerParty.MapEvent);
                Assert.Same(mapEvent, raiderParty.MapEvent);
                Assert.True(mapEvent.IsRaid);
                Assert.IsType<RaidEventComponent>(mapEvent.Component);
                Assert.Same(joinerParty.Party.MapEventSide, mapEvent.DefenderSide);
                Assert.Same(raiderParty.Party.MapEventSide, mapEvent.AttackerSide);
            });
        }
    }

    [Fact]
    public void RaidJoinEncounter_HelpAttackers_EntersLocalBattleEncounterAndRequestsServerJoin()
    {
        var client = Clients.First();
        client.Resolve<IControllerIdProvider>().SetControllerId("PlayerTwo");
        var (raiderHeroId, raiderMobilePartyId) = CreatePlayerHeroParty("PlayerOne");
        var (joinerHeroId, joinerMobilePartyId) = CreatePlayerHeroParty("PlayerTwo");
        var defenderTroopId = TestEnvironment.CreateRegisteredObject<CharacterObject>();
        var target = CreateVillageTarget();
        var joinerPartyId = GetPartyBaseId(joinerMobilePartyId);
        string? raidMapEventId = null;

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(raiderHeroId, out var raiderHero));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(joinerHeroId, out var joinerHero));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(raiderMobilePartyId, out var raiderParty));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(joinerMobilePartyId, out var joinerParty));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(target.SettlementId, out var settlement));
            Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(defenderTroopId, out var defenderTroop));

            using (new AllowedThread())
            {
                raiderParty.MemberRoster.AddToCounts(raiderHero.CharacterObject, 1);
                joinerParty.MemberRoster.AddToCounts(joinerHero.CharacterObject, 1);
                raiderHero.PartyBelongedTo = raiderParty;
                joinerHero.PartyBelongedTo = joinerParty;
            }

            var mapEvent = CreateHostileActionMapEvent(raiderParty.Party, settlement.Party, VillageHostileAction.Raid);
            var defenderParty = GameObjectCreator.CreateInitializedObject<MobileParty>();
            defenderParty.MemberRoster.AddToCounts(defenderTroop, 1);
            AddSyntheticMapEventParty(mapEvent.DefenderSide, defenderParty.Party);

            Assert.False(mapEvent.IsActiveSlowVillageRaid());
            Assert.True(Server.ObjectManager.TryGetId(mapEvent, out raidMapEventId));
        }, MapEventDisabledMethods);

        Assert.NotNull(raidMapEventId);

        client.NetworkSentMessages.Clear();
        var disabledMethods = MapEventDisabledMethods
            .Append(AccessTools.Method(typeof(GameMenu), nameof(GameMenu.ActivateGameMenu), new[] { typeof(string) }))
            .Append(AccessTools.Method(typeof(GameMenu), nameof(GameMenu.SwitchToMenu), new[] { typeof(string) }))
            // The E2E router delivers SendAll synchronously. Keep the outgoing request recorded, but avoid
            // server-side map-event mutation while native JoinBattleInternal is still building local state.
            .Append(AccessTools.Method(
                typeof(E2E.Tests.Environment.TestNetworkRouter),
                nameof(E2E.Tests.Environment.TestNetworkRouter.SendAll),
                new[] { typeof(LiteNetLib.NetPeer), typeof(IMessage) }))
            .ToList();

        client.Call(() =>
        {
            client.Resolve<IControllerIdProvider>().SetControllerId("PlayerTwo");
            Assert.True(client.ObjectManager.TryGetObject<MapEvent>(raidMapEventId!, out var mapEvent));
            Assert.True(client.ObjectManager.TryGetObject<Hero>(joinerHeroId, out var joinerHero));
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(joinerMobilePartyId, out var joinerParty));

            using (new AllowedThread())
            {
                Campaign.Current.MainParty = joinerParty;
                joinerHero.PartyBelongedTo = joinerParty;
                Game.Current.PlayerTroop = joinerHero.CharacterObject;
            }

            var encounter = ObjectHelper.SkipConstructor<PlayerEncounter>();
            encounter._mapEvent = mapEvent;
            encounter._encounteredParty = mapEvent.AttackerSide.LeaderParty;
            Campaign.Current.PlayerEncounter = encounter;

            Assert.Same(joinerParty, MobileParty.MainParty);
            Assert.Same(mapEvent, PlayerEncounter.EncounteredBattle);
            Assert.True(mapEvent.IsRaidHostileAction());

            var runOriginal = InvokeRaidJoinEncounterConsequencePrefix("game_menu_join_encounter_help_attackers_on_consequence");

            Assert.False(runOriginal);
            Assert.NotNull(PlayerEncounter.Current);
            Assert.Same(mapEvent, PlayerEncounter.Battle);
            Assert.Same(mapEvent, MobileParty.MainParty.MapEvent);
        }, disabledMethods);

        var request = client.NetworkSentMessages.GetMessages<NetworkRequestJoinBattle>().Single();
        Assert.Equal(raidMapEventId, request.MapEventId);
        Assert.Equal(joinerPartyId, request.PartyId);
        Assert.Equal(BattleSideEnum.Attacker, request.Side);
    }

    [Fact]
    public void SlowRaidMapEvent_AttackerJoinRequest_AddsJoiner()
    {
        var client = Clients.First();
        var (_, raiderMobilePartyId) = CreatePlayerHeroParty("PlayerOne");
        var (_, joinerMobilePartyId) = CreatePlayerHeroParty("PlayerTwo");
        var target = CreateVillageTarget();
        var joinerPartyId = GetPartyBaseId(joinerMobilePartyId);
        string? raidMapEventId = null;

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(raiderMobilePartyId, out var raiderParty));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(target.SettlementId, out var settlement));

            var mapEvent = CreateHostileActionMapEvent(raiderParty.Party, settlement.Party, VillageHostileAction.Raid);

            Assert.True(mapEvent.IsActiveSlowVillageRaid());
            Assert.True(Server.ObjectManager.TryGetId(mapEvent, out raidMapEventId));
        }, MapEventDisabledMethods);

        Assert.NotNull(raidMapEventId);

        Server.NetworkSentMessages.Clear();

        client.Call(() => client.Resolve<INetwork>().SendAll(new NetworkRequestJoinBattle(
            raidMapEventId!,
            joinerPartyId,
            BattleSideEnum.Attacker)), MapEventDisabledMethods);

        Assert.Contains(
            Server.NetworkSentMessages.GetMessages<NetworkAddInvolvedParties>(),
            message => message.MapEventId == raidMapEventId && message.MapEventPartyIds.Length > 0);
        Assert.Contains(
            Server.NetworkSentMessages.GetMessages<NetworkHidePvpPopup>(),
            message => message.PartyIds.Contains(joinerPartyId));
        AssertHostileActionJoinerPresent(Server, raidMapEventId!, joinerPartyId);
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(raidMapEventId!, out var mapEvent));
            Assert.True(mapEvent.IsActiveSlowVillageRaid());
        }, MapEventDisabledMethods);

        foreach (var instance in Clients)
        {
            AssertHostileActionJoinerPresent(instance, raidMapEventId!, joinerPartyId);
        }
    }

    [Fact]
    public void SlowRaidMapEvent_DefenderJoinRequest_IsRejected()
    {
        var client = Clients.First();
        var (_, raiderMobilePartyId) = CreatePlayerHeroParty("PlayerOne");
        var (_, joinerMobilePartyId) = CreatePlayerHeroParty("PlayerTwo");
        var target = CreateVillageTarget();
        var joinerPartyId = GetPartyBaseId(joinerMobilePartyId);
        string? raidMapEventId = null;

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(raiderMobilePartyId, out var raiderParty));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(target.SettlementId, out var settlement));

            var mapEvent = CreateHostileActionMapEvent(raiderParty.Party, settlement.Party, VillageHostileAction.Raid);

            Assert.True(mapEvent.IsActiveSlowVillageRaid());
            Assert.True(Server.ObjectManager.TryGetId(mapEvent, out raidMapEventId));
        }, MapEventDisabledMethods);

        Assert.NotNull(raidMapEventId);

        client.Call(() => client.Resolve<INetwork>().SendAll(new NetworkRequestJoinBattle(
            raidMapEventId!,
            joinerPartyId,
            BattleSideEnum.Defender)), MapEventDisabledMethods);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(raidMapEventId!, out var mapEvent));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(joinerMobilePartyId, out var joinerParty));
            Assert.True(mapEvent.IsActiveSlowVillageRaid());
            Assert.Null(joinerParty.MapEvent);
        }, MapEventDisabledMethods);

        foreach (var instance in Clients)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<MapEvent>(raidMapEventId!, out var _));
                Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(joinerMobilePartyId, out var joinerParty));
                Assert.Null(joinerParty.MapEvent);
            });
        }
    }

    [Theory]
    [InlineData(VillageHostileAction.Raid)]
    [InlineData(VillageHostileAction.ForceVolunteers)]
    [InlineData(VillageHostileAction.ForceSupplies)]
    public void SinglePlayerHostileAction_AttackMissionStart_IsAllowed(VillageHostileAction action)
    {
        var client = Clients.First();
        var hostileAction = CreateHostileActionWithOnePlayerParty(action);

        Server.NetworkSentMessages.Clear();

        client.Call(() => client.Resolve<INetwork>().SendAll(new NetworkBattleStartRequest(
            Guid.NewGuid().ToString(),
            (int)BattleStartMode.Mission,
            hostileAction.MapEventId,
            hostileAction.AttackerPartyId)), MapEventDisabledMethods);

        var start = Server.NetworkSentMessages.GetMessages<NetworkStartAttackMission>().Single();
        Assert.Equal(hostileAction.MapEventId, start.MapEventId);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(hostileAction.MapEventId, out var mapEvent));
            Assert.True(mapEvent.IsVillageHostileAction());
            Assert.False(mapEvent.IsVillageHostileActionWithMultiplePlayerParties());
        }, MapEventDisabledMethods);

        ServerBattleModeArbiter.Release(hostileAction.MapEventId);
    }

    [Fact]
    public void MultiPlayerRaid_AttackMissionStart_IsAllowed()
    {
        var client = Clients.First();
        var hostileAction = CreateHostileActionWithTwoPlayerParties(VillageHostileAction.Raid);

        Server.NetworkSentMessages.Clear();

        client.Call(() => client.Resolve<INetwork>().SendAll(new NetworkBattleStartRequest(
            Guid.NewGuid().ToString(),
            (int)BattleStartMode.Mission,
            hostileAction.MapEventId,
            hostileAction.AttackerPartyId)), MapEventDisabledMethods);

        var start = Server.NetworkSentMessages.GetMessages<NetworkStartAttackMission>().Single();
        Assert.Equal(hostileAction.MapEventId, start.MapEventId);

        var mode = Server.NetworkSentMessages.GetMessages<NetworkBattleModeSet>().Single();
        Assert.Equal(hostileAction.MapEventId, mode.MapEventId);
        Assert.Equal((int)BattleStartMode.Mission, mode.Mode);

        var reply = Server.NetworkSentMessages.GetMessages<NetworkBattleStartReply>().Single();
        Assert.True(reply.Accepted);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(hostileAction.MapEventId, out var mapEvent));
            Assert.True(mapEvent.IsRaidHostileAction());
            Assert.True(mapEvent.IsVillageHostileActionWithMultiplePlayerParties());
            Assert.False(mapEvent.IsUnsupportedMultiPlayerHostileAction());
        }, MapEventDisabledMethods);

        ServerBattleModeArbiter.Release(hostileAction.MapEventId);
    }

    [Theory]
    [InlineData(VillageHostileAction.ForceVolunteers)]
    [InlineData(VillageHostileAction.ForceSupplies)]
    public void MultiPlayerNonRaidHostileAction_AttackMissionStart_IsRejected(VillageHostileAction action)
    {
        var client = Clients.First();
        var hostileAction = CreateHostileActionWithTwoPlayerParties(action);

        Server.NetworkSentMessages.Clear();

        client.Call(() => client.Resolve<INetwork>().SendAll(new NetworkBattleStartRequest(
            Guid.NewGuid().ToString(),
            (int)BattleStartMode.Mission,
            hostileAction.MapEventId,
            hostileAction.AttackerPartyId)), MapEventDisabledMethods);

        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkStartAttackMission>());

        var reply = Server.NetworkSentMessages.GetMessages<NetworkBattleStartReply>().Single();
        Assert.False(reply.Accepted);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(hostileAction.MapEventId, out var mapEvent));
            Assert.True(mapEvent.IsVillageHostileActionWithMultiplePlayerParties());
            Assert.True(mapEvent.IsUnsupportedMultiPlayerHostileAction());
        }, MapEventDisabledMethods);
    }

    [Fact]
    public void MultiPlayerRaid_BattleSimulationStart_IsAllowed()
    {
        var client = Clients.First();
        var hostileAction = CreateHostileActionWithTwoPlayerParties(VillageHostileAction.Raid);

        Server.NetworkSentMessages.Clear();

        client.Call(() => client.Resolve<INetwork>().SendAll(new NetworkBattleStartRequest(
            Guid.NewGuid().ToString(),
            (int)BattleStartMode.Simulation,
            hostileAction.MapEventId,
            hostileAction.AttackerPartyId)), MapEventDisabledMethods);

        var open = Server.NetworkSentMessages.GetMessages<NetworkOpenBattleSimulation>().Single();
        Assert.Equal(hostileAction.MapEventId, open.MapEventId);

        var mode = Server.NetworkSentMessages.GetMessages<NetworkBattleModeSet>().Single();
        Assert.Equal(hostileAction.MapEventId, mode.MapEventId);
        Assert.Equal((int)BattleStartMode.Simulation, mode.Mode);

        var reply = Server.NetworkSentMessages.GetMessages<NetworkBattleStartReply>().Single();
        Assert.True(reply.Accepted);
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkBattleSimulationFinished>());

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(hostileAction.MapEventId, out var mapEvent));
            Assert.True(mapEvent.IsRaid);
            Assert.True(mapEvent.IsVillageHostileActionWithMultiplePlayerParties());
            Assert.True(mapEvent.BattleObserver is ForwardingBattleObserver);
        }, MapEventDisabledMethods);

        ServerBattleModeArbiter.Release(hostileAction.MapEventId);
    }

    [Theory]
    [InlineData(VillageHostileAction.ForceVolunteers)]
    [InlineData(VillageHostileAction.ForceSupplies)]
    public void MultiPlayerNonRaidHostileAction_BattleSimulationStart_IsRejected(VillageHostileAction action)
    {
        var client = Clients.First();
        var hostileAction = CreateHostileActionWithTwoPlayerParties(action);

        Server.NetworkSentMessages.Clear();

        client.Call(() => client.Resolve<INetwork>().SendAll(new NetworkBattleStartRequest(
            Guid.NewGuid().ToString(),
            (int)BattleStartMode.Simulation,
            hostileAction.MapEventId,
            hostileAction.AttackerPartyId)), MapEventDisabledMethods);

        var finished = Server.NetworkSentMessages.GetMessages<NetworkBattleSimulationFinished>().Single();
        Assert.Equal(hostileAction.MapEventId, finished.MapEventId);
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkOpenBattleSimulation>());

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(hostileAction.MapEventId, out var mapEvent));
            Assert.True(mapEvent.IsVillageHostileActionWithMultiplePlayerParties());
            Assert.False(mapEvent.BattleObserver is ForwardingBattleObserver);
        }, MapEventDisabledMethods);
    }

    [Fact]
    public void RaidSimulation_WhenSecondPlayerJoins_OpensSimulationForJoiner()
    {
        var client = Clients.First();
        var (raiderHeroId, raiderMobilePartyId) = CreatePlayerHeroParty("PlayerOne");
        var (joinerHeroId, joinerMobilePartyId) = CreatePlayerHeroParty("PlayerTwo");
        var defenderTroopId = TestEnvironment.CreateRegisteredObject<CharacterObject>();
        var target = CreateVillageTarget();
        var joinerPartyId = GetPartyBaseId(joinerMobilePartyId);
        string? mapEventId = null;

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(raiderHeroId, out var raiderHero));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(joinerHeroId, out var joinerHero));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(raiderMobilePartyId, out var raiderParty));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(joinerMobilePartyId, out var joinerParty));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(target.SettlementId, out var settlement));
            Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(defenderTroopId, out var defenderTroop));

            using (new AllowedThread())
            {
                raiderParty.MemberRoster.AddToCounts(raiderHero.CharacterObject, 1);
                joinerParty.MemberRoster.AddToCounts(joinerHero.CharacterObject, 1);
                raiderHero.PartyBelongedTo = raiderParty;
                joinerHero.PartyBelongedTo = joinerParty;
            }

            var raidMapEvent = CreateHostileActionMapEvent(raiderParty.Party, settlement.Party, VillageHostileAction.Raid);
            Assert.NotNull(raidMapEvent);
            raidMapEvent.MapEventVisual = MockMapEventVisual();
            raidMapEvent.BattleObserver = new ForwardingBattleObserver(Server.ObjectManager);

            var defenderParty = GameObjectCreator.CreateInitializedObject<MobileParty>();
            defenderParty.MemberRoster.AddToCounts(defenderTroop, 1);
            AddSyntheticMapEventParty(raidMapEvent.DefenderSide, defenderParty.Party);

            Assert.False(raidMapEvent.IsVillageHostileActionWithMultiplePlayerParties());
            Assert.True(Server.ObjectManager.TryGetId(raidMapEvent, out mapEventId));
        }, MapEventDisabledMethods);

        Assert.NotNull(mapEventId);

        Server.NetworkSentMessages.Clear();

        client.Call(() => client.Resolve<INetwork>().SendAll(new NetworkRequestJoinBattle(
            mapEventId!,
            joinerPartyId,
            BattleSideEnum.Attacker)), MapEventDisabledMethods);

        var open = Server.NetworkSentMessages.GetMessages<NetworkOpenBattleSimulation>().Single();
        Assert.Equal(mapEventId, open.MapEventId);

        var warDeclared = Server.NetworkSentMessages.GetMessages<NetworkDeclareWar>().Single();
        Assert.Equal(GetMobilePartyMapFactionId(Server, joinerMobilePartyId), warDeclared.Faction1Id);
        Assert.Equal(target.OwnerFactionId, warDeclared.Faction2Id);

        AssertWarDeclared(Server, joinerMobilePartyId, target.OwnerFactionId);
        foreach (var syncedClient in Clients)
        {
            AssertWarDeclared(syncedClient, joinerMobilePartyId, target.OwnerFactionId);
        }

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventId!, out var mapEvent));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(joinerMobilePartyId, out var joinerParty));
            Assert.Same(mapEvent, joinerParty.MapEvent);
            Assert.True(mapEvent.IsVillageHostileActionWithMultiplePlayerParties());
            Assert.True(mapEvent.BattleObserver is ForwardingBattleObserver);
            Assert.False(mapEvent.HasWinner);
        }, MapEventDisabledMethods);
    }

    [Theory]
    [InlineData(VillageHostileAction.Raid)]
    [InlineData(VillageHostileAction.ForceVolunteers)]
    [InlineData(VillageHostileAction.ForceSupplies)]
    public void HostileActionJoinerLeave_RemovesOnlyJoinerAndKeepsEvent(VillageHostileAction action)
    {
        var client = Clients.First();
        var hostileAction = CreateHostileActionWithOnePlayerParty(action);
        var (_, joinerMobilePartyId) = CreatePlayerHeroParty("PlayerTwo");
        var joinerPartyId = GetPartyBaseId(joinerMobilePartyId);

        Server.NetworkSentMessages.Clear();

        client.Call(() => client.Resolve<INetwork>().SendAll(new NetworkRequestJoinBattle(
            hostileAction.MapEventId,
            joinerPartyId,
            BattleSideEnum.Attacker)), MapEventDisabledMethods);

        var warDeclared = Server.NetworkSentMessages.GetMessages<NetworkDeclareWar>().Single();
        Assert.Equal(GetMobilePartyMapFactionId(Server, joinerMobilePartyId), warDeclared.Faction1Id);
        Assert.Equal(hostileAction.OwnerFactionId, warDeclared.Faction2Id);

        AssertWarDeclared(Server, joinerMobilePartyId, hostileAction.OwnerFactionId);
        foreach (var syncedClient in Clients)
        {
            AssertWarDeclared(syncedClient, joinerMobilePartyId, hostileAction.OwnerFactionId);
        }

        AssertHostileActionJoinerPresent(Server, hostileAction.MapEventId, joinerPartyId);
        foreach (var joinedClient in Clients)
        {
            AssertHostileActionJoinerPresent(joinedClient, hostileAction.MapEventId, joinerPartyId);
        }

        Server.NetworkSentMessages.Clear();
        client.Call(() => client.Resolve<INetwork>().SendAll(new NetworkRequestLeaveBattle(
            joinerPartyId)), MapEventDisabledMethods);

        var left = Server.NetworkSentMessages.GetMessages<NetworkPartyLeftBattle>().Single();
        Assert.Equal(joinerPartyId, left.PartyId);

        AssertHostileActionJoinerLeft(Server, hostileAction.MapEventId, hostileAction.AttackerPartyId, joinerPartyId);
        foreach (var leftClient in Clients)
        {
            AssertHostileActionJoinerLeft(leftClient, hostileAction.MapEventId, hostileAction.AttackerPartyId, joinerPartyId);
        }
    }

    [Fact]
    public void VillageRecovery_RegisterEventsRunsOnlyOnServer()
    {
        Server.Call(() => Assert.True(InvokeVillageHealRegisterEventsPrefix()));

        foreach (var client in Clients)
        {
            client.Call(() => Assert.False(InvokeVillageHealRegisterEventsPrefix()));
        }
    }

    private void AssertHostileActionJoinerPresent(
        EnvironmentInstance instance,
        string mapEventId,
        string joinerPartyId)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<MapEvent>(mapEventId, out var mapEvent));
            Assert.True(instance.ObjectManager.TryGetObject<PartyBase>(joinerPartyId, out var joinerParty));

            Assert.Same(mapEvent, joinerParty.MapEvent);
            Assert.True(mapEvent.IsVillageHostileActionWithMultiplePlayerParties());
        }, MapEventDisabledMethods);
    }

    private void AssertHostileActionJoinerLeft(
        EnvironmentInstance instance,
        string mapEventId,
        string attackerPartyId,
        string joinerPartyId)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<MapEvent>(mapEventId, out var mapEvent));
            Assert.True(instance.ObjectManager.TryGetObject<PartyBase>(attackerPartyId, out var attackerParty));
            Assert.True(instance.ObjectManager.TryGetObject<PartyBase>(joinerPartyId, out var joinerParty));

            Assert.Same(mapEvent, attackerParty.MapEvent);
            Assert.Null(joinerParty.MapEventSide);
            Assert.True(mapEvent.IsVillageHostileAction());
            Assert.False(mapEvent.IsVillageHostileActionWithMultiplePlayerParties());
        }, MapEventDisabledMethods);
    }

    private void AssertRaidPartyMovedToVillageGate(
        EnvironmentInstance instance,
        string mobilePartyId,
        string settlementId)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(mobilePartyId, out var mobileParty));
            Assert.True(instance.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));

            Assert.Null(mobileParty.CurrentSettlement);
            Assert.True(mobileParty.Position.Distance(settlement.GatePosition) < 0.001f);
            Assert.True(mobileParty.MoveTargetPoint.Distance(mobileParty.Position) < 0.001f);
            Assert.Equal(MoveModeType.Hold, mobileParty.PartyMoveMode);
        }, MapEventDisabledMethods);
    }

    private void AssertRaidFinalizedOutcome(
        EnvironmentInstance instance,
        string mapEventId,
        string settlementId,
        string villageId,
        Village.VillageStates expectedVillageState,
        float expectedSettlementHitPoints)
    {
        instance.Call(() =>
        {
            Assert.False(instance.ObjectManager.TryGetObject<MapEvent>(mapEventId, out var _));
            Assert.True(instance.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));
            Assert.True(instance.ObjectManager.TryGetObject<Village>(villageId, out var village));

            Assert.Equal(expectedVillageState, village.VillageState);
            Assert.Equal(expectedSettlementHitPoints, settlement.SettlementHitPoints, 3);
        });
    }

    private void AssertVillageStateAndHitPoints(
        EnvironmentInstance instance,
        string settlementId,
        string villageId,
        Village.VillageStates expectedVillageState,
        float expectedSettlementHitPoints)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));
            Assert.True(instance.ObjectManager.TryGetObject<Village>(villageId, out var village));

            Assert.Equal(expectedVillageState, village.VillageState);
            Assert.Equal(expectedSettlementHitPoints, settlement.SettlementHitPoints, 3);
        });
    }

    private void AssertHostileActionMapEvent(
        EnvironmentInstance instance,
        string mapEventId,
        string componentId,
        Type expectedComponentType,
        string villageId,
        Village.VillageStates expectedVillageState)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<MapEvent>(mapEventId, out var mapEvent));
            Assert.True(instance.ObjectManager.TryGetObject<MapEventComponent>(componentId, out var component));
            Assert.True(instance.ObjectManager.TryGetObject<Village>(villageId, out var village));

            Assert.Same(component, mapEvent.Component);
            Assert.IsType(expectedComponentType, component);
            Assert.Equal(expectedVillageState, village.VillageState);
        });
    }

    private void AssertRaidProgressOutcome(
        EnvironmentInstance instance,
        string mapEventId,
        string componentId,
        string mobilePartyId,
        string settlementId,
        string villageId,
        string itemId)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<MapEvent>(mapEventId, out var mapEvent));
            Assert.True(instance.ObjectManager.TryGetObject<MapEventComponent>(componentId, out var component));
            Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(mobilePartyId, out var mobileParty));
            Assert.True(instance.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));
            Assert.True(instance.ObjectManager.TryGetObject<Village>(villageId, out var village));
            Assert.True(instance.ObjectManager.TryGetObject<ItemObject>(itemId, out var item));

            var raidComponent = Assert.IsType<RaidEventComponent>(component);
            Assert.Same(component, mapEvent.Component);
            Assert.True(mapEvent.WasEverInLootingPhase);
            Assert.True(raidComponent.RaidDamage > 0);
            Assert.True(settlement.SettlementHitPoints < 1f);
            Assert.True(village.Hearth < 100f);
            Assert.NotNull(raidComponent._raidProductionRewards);
            Assert.True(GetItemAmount(raidComponent._raidProductionRewards, item) > 0);
        });
    }

    private void AssertForceSuppliesOutcome(
        EnvironmentInstance instance,
        string mobilePartyId,
        string settlementId,
        string itemId)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(mobilePartyId, out var mobileParty));
            Assert.True(instance.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));
            Assert.True(instance.ObjectManager.TryGetObject<ItemObject>(itemId, out var item));

            Assert.Equal(40, GetItemAmount(mobileParty.Party, item));
            Assert.Equal(80, mobileParty.LeaderHero.Gold);
            Assert.Equal(0.2f, settlement.SettlementHitPoints, 3);
        });
    }

    private void AssertForceSuppliesGoldAndHitPointsOutcome(
        EnvironmentInstance instance,
        string mobilePartyId,
        string settlementId)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(mobilePartyId, out var mobileParty));
            Assert.True(instance.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));

            Assert.Equal(80, mobileParty.LeaderHero.Gold);
            Assert.Equal(0.2f, settlement.SettlementHitPoints, 3);
        });
    }

    private void AssertForceVolunteersOutcome(
        EnvironmentInstance instance,
        string mobilePartyId,
        string settlementId,
        string villageId,
        string troopId)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(mobilePartyId, out var mobileParty));
            Assert.True(instance.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));
            Assert.True(instance.ObjectManager.TryGetObject<Village>(villageId, out var village));
            Assert.True(instance.ObjectManager.TryGetObject<CharacterObject>(troopId, out var troop));

            Assert.Equal(3, mobileParty.MemberRoster.GetTroopCount(troop));
            Assert.Equal(0.2f, settlement.SettlementHitPoints, 3);
            Assert.Equal(89f, village.Hearth);
        });
    }

    private static int GetItemAmount(PartyBase party, ItemObject itemObject)
    {
        foreach (var item in party.ItemRoster)
        {
            if (item.EquipmentElement.Item == itemObject)
                return item.Amount;
        }

        return 0;
    }

    private static int GetItemAmount(ItemRoster roster, ItemObject itemObject)
    {
        foreach (var item in roster)
        {
            if (item.EquipmentElement.Item == itemObject)
                return item.Amount;
        }

        return 0;
    }

    private static int GetItemAmount(Dictionary<ItemObject, float> rewards, ItemObject itemObject)
    {
        return rewards.TryGetValue(itemObject, out var amount) ? (int)amount : 0;
    }

    private void RemoveNativeItemObjectFromObjectManagers(string stringId)
    {
        RemoveNativeItemObjectFromObjectManager(Server, stringId);

        foreach (var client in Clients)
        {
            RemoveNativeItemObjectFromObjectManager(client, stringId);
        }
    }

    private static void RemoveNativeItemObjectFromObjectManager(EnvironmentInstance instance, string stringId)
    {
        instance.Call(() =>
        {
            var item = GetNativeItemObject(stringId);
            if (item != null)
                instance.ObjectManager.Remove(item);
        });
    }

    private static ItemObject GetNativeItemObject(string stringId)
    {
        return MBObjectManager.Instance.GetObject<ItemObject>(stringId) ??
               MBObjectManager.Instance.GetObjectTypeList<ItemObject>().FirstOrDefault(item => item.StringId == stringId);
    }

    private void AddServerCooldown(string settlementId)
    {
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));

            Server.Resolve<IVillageHostileActionInterface>().ApplyCooldowns(new[]
            {
                new VillageHostileActionCooldownData(settlementId, CampaignTime.DaysFromNow(10).NumTicks),
            });
        });
    }

    private void AssertCooldownBroadcast(string settlementId)
    {
        var cooldowns = Server.NetworkSentMessages.GetMessages<NetworkVillageHostileActionCooldowns>().Single();
        Assert.Contains(cooldowns.Cooldowns, c => c.SettlementId == settlementId);
    }

    private void AssertCooldownSynced(EnvironmentInstance instance, string settlementId)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));
            Assert.True(instance.Resolve<IVillageHostileActionInterface>().TryGetForceActionCooldown(settlement, out var cooldownUntil));
            Assert.False(cooldownUntil.IsPast);
        });
    }

    private static string GetMobilePartyMapFactionId(EnvironmentInstance instance, string mobilePartyId)
    {
        string factionId = null;
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(mobilePartyId, out var mobileParty));
            var mapFaction = mobileParty.MapFaction?.MapFaction;
            Assert.NotNull(mapFaction);
            Assert.True(instance.ObjectManager.TryGetId(mapFaction, out factionId));
        });

        return factionId;
    }

    private void AssertWarDeclared(EnvironmentInstance instance, string mobilePartyId, string defenderFactionId)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(mobilePartyId, out var mobileParty));
            Assert.True(TryGetFaction(instance, defenderFactionId, out var defenderFaction));
            var attackerFaction = mobileParty.MapFaction?.MapFaction;
            var defenderMapFaction = defenderFaction.MapFaction;
            Assert.NotNull(attackerFaction);
            Assert.NotNull(defenderMapFaction);
            var shallowStance = Campaign.Current.Models.DiplomacyModel.GetShallowDiplomaticStance(attackerFaction, defenderMapFaction);
            var stanceLink = FactionManager.Instance.GetStanceLinkInternal(attackerFaction, defenderMapFaction);
            Assert.True(
                VillageHostileFactionStanceHelper.HasWarStance(attackerFaction, defenderMapFaction),
                $"Expected {GetFactionDebugName(instance, attackerFaction)} to be at war with {GetFactionDebugName(instance, defenderMapFaction)}. AttackerEliminated={attackerFaction.IsEliminated}, DefenderEliminated={defenderMapFaction.IsEliminated}, Shallow={shallowStance?.ToString() ?? "null"}, LinkWar={stanceLink.IsAtWar}, LinkStance={stanceLink.StanceType}, AttackerWarsContainsDefender={attackerFaction.FactionsAtWarWith?.Contains(defenderMapFaction) == true}, DefenderWarsContainsAttacker={defenderMapFaction.FactionsAtWarWith?.Contains(attackerFaction) == true}");
        });
    }

    private static string GetFactionDebugName(EnvironmentInstance instance, IFaction faction)
    {
        if (instance.ObjectManager.TryGetId(faction, out var factionId))
            return $"{faction.GetType().Name}:{factionId}";

        return faction.GetType().Name;
    }

    private static bool TryGetFaction(EnvironmentInstance instance, string factionId, out IFaction faction)
    {
        if (instance.ObjectManager.TryGetObject<Kingdom>(factionId, out var kingdom))
        {
            faction = kingdom;
            return true;
        }

        if (instance.ObjectManager.TryGetObject<Clan>(factionId, out var clan))
        {
            faction = clan;
            return true;
        }

        faction = null;
        return false;
    }

    private static bool InvokeMapEventUpdatePrefix(MapEvent mapEvent)
    {
        var patchType = AccessTools.TypeByName("GameInterface.Services.MapEvents.Patches.MapEventPatches");
        var prefix = AccessTools.Method(patchType, "PrefixUpdate");
        Assert.NotNull(prefix);

        return (bool)prefix.Invoke(null, new object[] { mapEvent })!;
    }

    private static bool InvokeCanPartyJoinBattlePostfix(MapEvent mapEvent, PartyBase party, bool initialResult)
    {
        var patchType = AccessTools.TypeByName("GameInterface.Services.MapEvents.Patches.InteractionPatches");
        var postfix = AccessTools.Method(patchType, "Postfix_CanPartyJoinBattle");
        Assert.NotNull(postfix);

        var args = new object[] { mapEvent, party, initialResult };
        postfix.Invoke(null, args);
        return (bool)args[2];
    }

    private static bool InvokeStartSettlementEncounterPrefix(MobileParty attackerParty, Settlement settlement)
    {
        var patchType = AccessTools.TypeByName("GameInterface.Services.MapEvents.Patches.EncounterManagerPatches");
        var prefix = AccessTools.Method(patchType, "Prefix");
        Assert.NotNull(prefix);

        return (bool)prefix.Invoke(null, new object[] { attackerParty, settlement })!;
    }

    private static bool InvokeStartPartyEncounterPrefix(PartyBase attackerParty, PartyBase defenderParty)
    {
        var patchType = AccessTools.TypeByName("GameInterface.Services.MapEvents.Patches.EncounterManagerPatches");
        var prefix = AccessTools.Method(patchType, "StartPartyEncounterPrefix");
        Assert.NotNull(prefix);

        return (bool)prefix.Invoke(null, new object[] { attackerParty, defenderParty })!;
    }

    private static bool InvokeRestartPlayerEncounterPrefix(PartyBase attackerParty, PartyBase defenderParty)
    {
        var patchType = AccessTools.TypeByName("GameInterface.Services.MapEvents.Patches.EncounterManagerPatches");
        var prefix = AccessTools.Method(patchType, "RestartPlayerEncounterPrefix");
        Assert.NotNull(prefix);

        return (bool)prefix.Invoke(null, new object[] { attackerParty, defenderParty })!;
    }

    private static void InvokeVillageHealDailyTick(Settlement settlement)
    {
        var behavior = new VillageHealCampaignBehavior();
        var method = AccessTools.Method(typeof(VillageHealCampaignBehavior), "DailyTickSettlement");
        Assert.NotNull(method);

        method.Invoke(behavior, new object[] { settlement });
    }

    private static bool InvokeRaidJoinEncounterConsequencePrefix(string methodName)
    {
        var originalMethod = AccessTools.Method(typeof(EncounterGameMenuBehavior), methodName);
        Assert.NotNull(originalMethod);

        var patchType = AccessTools.TypeByName("GameInterface.Services.MapEvents.Patches.RaidJoinEncounterPatch");
        var prefix = AccessTools.Method(patchType, "Prefix");
        Assert.NotNull(prefix);

        return (bool)prefix.Invoke(null, new object[] { originalMethod })!;
    }
    private static bool InvokeVillageHealRegisterEventsPrefix()
    {
        var patchType = AccessTools.TypeByName("GameInterface.Services.Villages.Patches.DisableVillageHealCampaignBehavior");
        var prefix = AccessTools.Method(patchType, "Prefix");
        Assert.NotNull(prefix);

        return (bool)prefix.Invoke(null, Array.Empty<object>())!;
    }

    private void RegisterPeer(EnvironmentInstance client, string controllerId)
    {
        EnsurePeerEndpoint(client);
        client.Resolve<IControllerIdProvider>().SetControllerId(controllerId);
        Server.SimulateMessage(this, new PlayerConnected(client.NetPeer));
        Server.SimulateMessage(client.NetPeer, new NetworkClientValidate(controllerId));
    }

    private static void EnsurePeerEndpoint(EnvironmentInstance client)
    {
        var endPoint = (IPEndPoint)client.NetPeer;
        if (endPoint.Address != null)
            return;

        endPoint.Address = IPAddress.Loopback;
        endPoint.Port = 1 + (Interlocked.Increment(ref peerPortCounter) % 60000);
    }

    private VillageTarget CreateVillageTarget()
    {
        var settlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
        var villageId = TestEnvironment.CreateRegisteredObject<Village>();
        var boundSettlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
        var boundTownId = TestEnvironment.CreateRegisteredObject<Town>();
        var boundOwnerClanId = TestEnvironment.CreateRegisteredObject<Clan>();
        var boundOwnerKingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var boundOwnerHeroId = TestEnvironment.CreateRegisteredObject<Hero>();
        string? settlementPartyId = null;
        string? ownerFactionId = null;

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));
            Assert.True(Server.ObjectManager.TryGetObject<Village>(villageId, out var village));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(boundSettlementId, out var boundSettlement));
            Assert.True(Server.ObjectManager.TryGetObject<Town>(boundTownId, out var boundTown));
            Assert.True(Server.ObjectManager.TryGetObject<Clan>(boundOwnerClanId, out var boundOwnerClan));
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(boundOwnerKingdomId, out var boundOwnerKingdom));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(boundOwnerHeroId, out var boundOwnerHero));

            using (new AllowedThread())
            {
                boundSettlement.SetSettlementComponent(boundTown);
                boundOwnerHero.Clan = boundOwnerClan;
                boundOwnerClan.Kingdom = boundOwnerKingdom;
                boundOwnerClan.SetLeader(boundOwnerHero);
                boundOwnerKingdom.RulingClan = boundOwnerClan;
                boundTown.OwnerClan = boundOwnerClan;
                boundTown.IsOwnerUnassigned = false;
                settlement.Village = village;
                settlement.SetSettlementComponent(village);
                settlement.Party = new PartyBase(settlement);
                village.Bound = boundSettlement;
                village.VillageState = Village.VillageStates.Normal;
                village.Hearth = 100f;
            }

            Assert.True(Server.ObjectManager.AddNewObject(settlement.Party, out settlementPartyId));
            var ownerFaction = settlement.MapFaction?.MapFaction;
            Assert.NotNull(ownerFaction);
            Assert.True(Server.ObjectManager.TryGetId(ownerFaction, out ownerFactionId));
        });

        Assert.NotNull(settlementPartyId);
        foreach (var client in Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));
                Assert.True(client.ObjectManager.TryGetObject<Clan>(boundOwnerClanId, out var boundOwnerClan));
                Assert.True(client.ObjectManager.TryGetObject<Hero>(boundOwnerHeroId, out var boundOwnerHero));

                using (new AllowedThread())
                {
                    boundOwnerHero.Clan = boundOwnerClan;
                    boundOwnerClan.SetLeader(boundOwnerHero);
                }

                if (!client.ObjectManager.TryGetObject<PartyBase>(settlementPartyId!, out var settlementParty))
                {
                    using (new AllowedThread())
                    {
                        settlementParty = new PartyBase(settlement);
                        settlement.Party = settlementParty;
                    }

                    Assert.True(client.ObjectManager.AddExisting(settlementPartyId!, settlementParty));
                    return;
                }

                using (new AllowedThread())
                {
                    settlement.Party = settlementParty;
                }
            });
        }

        return new VillageTarget(settlementId, villageId, settlementPartyId!, ownerFactionId!);
    }

    private void RequestConversation(EnvironmentInstance client, string attackerPartyId, string defenderPartyId)
    {
        client.Call(() => client.Resolve<INetwork>().SendAll(new NetworkRequestConversation(
            defenderPartyId,
            attackerPartyId,
            forcePlayerOutFromSettlement: false,
            ConversationRestartSource.PlayerEncounter)));
    }

    private void RequestHostileAction(
        EnvironmentInstance client,
        VillageHostileAction action,
        string mobilePartyId,
        string settlementId)
    {
        var disabledMethods = MapEventDisabledMethods
            .Append(AccessTools.Method(typeof(BeHostileAction), nameof(BeHostileAction.ApplyEncounterHostileAction)))
            .Append(AccessTools.Method(typeof(GameMenu), nameof(GameMenu.SwitchToMenu)))
            .ToList();

        client.Call(() => client.Resolve<INetwork>().SendAll(new NetworkRequestVillageHostileAction(
            action,
            mobilePartyId,
            settlementId,
            client.Resolve<IControllerIdProvider>().ControllerId)), disabledMethods);
    }

    private static void SetMapEventCreationTimeout(EnvironmentInstance instance, TimeSpan timeout)
    {
        instance.Call(() =>
        {
            var config = new Mock<INetworkConfig>();
            config.SetupGet(x => x.ObjectCreationTimeout).Returns(timeout);

            var coordinator = instance.Resolve<MapEventCreationCoordinator>();
            var configurationField = AccessTools.Field(typeof(MapEventCreationCoordinator), "configuration");
            Assert.NotNull(configurationField);
            configurationField.SetValue(coordinator, config.Object);
        });
    }

    private void AssertCanStartHostileAction(string mobilePartyId, string settlementId, VillageHostileAction action)
    {
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(mobilePartyId, out var mobileParty));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));

            Assert.True(Server.Resolve<IVillageHostileActionInterface>().CanStartHostileAction(
                mobileParty,
                settlement,
                action,
                out var reason));
            Assert.Equal(VillageHostileActionDeniedReason.Invalid, reason);
        });
    }

    private void ApproveMapEventStart(string mobilePartyId, string settlementId, VillageHostileAction action)
    {
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(mobilePartyId, out var mobileParty));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));

            Server.Resolve<IVillageHostileActionInterface>().ApproveMapEventStart(mobileParty.Party, settlement, action);
        });
    }

    private (bool Approved, VillageHostileActionDeniedReason Reason) ConsumeApprovedMapEventStart(
        string mobilePartyId,
        string settlementPartyId,
        BattleCreationFlags flags)
    {
        var approved = false;
        var reason = VillageHostileActionDeniedReason.Invalid;

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(mobilePartyId, out var mobileParty));
            Assert.True(Server.ObjectManager.TryGetObject<PartyBase>(settlementPartyId, out var settlementParty));

            approved = Server.Resolve<IVillageHostileActionInterface>().TryConsumeApprovedMapEventStart(
                mobileParty.Party,
                settlementParty,
                flags,
                out reason);
        });

        return (approved, reason);
    }

    private string GetPartyBaseId(string mobilePartyId)
    {
        string? partyBaseId = null;

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(mobilePartyId, out var party));
            Assert.True(Server.ObjectManager.TryGetId(party.Party, out partyBaseId));
        });

        Assert.NotNull(partyBaseId);
        return partyBaseId!;
    }

    private string RegisterMobilePartyItemRoster(string mobilePartyId)
    {
        string? itemRosterId = null;

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(mobilePartyId, out var mobileParty));
            Assert.NotNull(mobileParty.Party?.ItemRoster);

            itemRosterId = Server.ObjectManager.TryGetId(mobileParty.Party.ItemRoster, out var existingItemRosterId)
                ? existingItemRosterId
                : $"{nameof(ItemRoster)}_{mobileParty.StringId}";
        });

        Assert.NotNull(itemRosterId);
        RegisterMobilePartyItemRoster(Server, mobilePartyId, itemRosterId!);
        foreach (var client in Clients)
        {
            RegisterMobilePartyItemRoster(client, mobilePartyId, itemRosterId!);
        }

        return itemRosterId!;
    }

    private static void RegisterMobilePartyItemRoster(
        EnvironmentInstance instance,
        string mobilePartyId,
        string itemRosterId)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(mobilePartyId, out var mobileParty));
            Assert.NotNull(mobileParty.Party?.ItemRoster);

            var itemRoster = mobileParty.Party.ItemRoster;
            if (instance.ObjectManager.TryGetId(itemRoster, out var existingItemRosterId))
            {
                if (existingItemRosterId == itemRosterId)
                    return;

                Assert.True(instance.ObjectManager.Remove(itemRoster));
            }

            if (instance.ObjectManager.Contains(itemRosterId))
            {
                Assert.True(instance.ObjectManager.TryGetObject<ItemRoster>(itemRosterId, out var registeredItemRoster));
                if (ReferenceEquals(registeredItemRoster, itemRoster))
                    return;

                Assert.True(instance.ObjectManager.Remove(registeredItemRoster));
            }

            Assert.True(instance.ObjectManager.AddExisting(itemRosterId, itemRoster));
        });
    }

    private static MapEvent CreateHostileActionMapEvent(PartyBase attacker, PartyBase defender, VillageHostileAction action)
    {
        var mapEvent = GameObjectCreator.CreateInitializedObject<MapEvent>();
        mapEvent.MapEventVisual = MockMapEventVisual();

        MapEventComponent component;
        MapEvent.BattleTypes battleType;
        switch (action)
        {
            case VillageHostileAction.Raid:
                component = new RaidEventComponent(mapEvent);
                battleType = MapEvent.BattleTypes.Raid;
                break;
            case VillageHostileAction.ForceVolunteers:
                component = new ForceVolunteersEventComponent(mapEvent);
                battleType = (MapEvent.BattleTypes)3;
                break;
            case VillageHostileAction.ForceSupplies:
                component = new ForceSuppliesEventComponent(mapEvent);
                battleType = (MapEvent.BattleTypes)4;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(action), action, null);
        }

        mapEvent.Initialize(attacker, defender, component, battleType);
        mapEvent.MapEventSettlement = defender.Settlement;
        mapEvent.Position = defender.Position;
        mapEvent.State = MapEventState.Wait;

        SetVillageStateForHostileAction(defender.Settlement, action);
        return mapEvent;
    }

    private static void SetVillageStateForHostileAction(Settlement settlement, VillageHostileAction action)
    {
        switch (action)
        {
            case VillageHostileAction.Raid:
                settlement.Village.VillageState = Village.VillageStates.BeingRaided;
                break;
            case VillageHostileAction.ForceVolunteers:
                settlement.Village.VillageState = Village.VillageStates.ForcedForVolunteers;
                break;
            case VillageHostileAction.ForceSupplies:
                settlement.Village.VillageState = Village.VillageStates.ForcedForSupplies;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(action), action, null);
        }
    }

    private static void AddSyntheticMapEventParty(MapEventSide side, PartyBase party)
    {
        party._mapEventSide = side;
        var mapEventParty = new MapEventParty(party);
        side._battleParties.Add(mapEventParty);
        MessageBroker.Instance.Publish(side, new MapEventPartyAdded(side, mapEventParty));
    }

    private RaidMapEventContext CreateHostileActionWithOnePlayerParty(VillageHostileAction action)
    {
        var (raiderHeroId, raiderMobilePartyId) = CreatePlayerHeroParty("PlayerOne");
        var defenderTroopId = TestEnvironment.CreateRegisteredObject<CharacterObject>();
        var target = CreateVillageTarget();
        var raiderPartyId = GetPartyBaseId(raiderMobilePartyId);
        string? mapEventId = null;

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(raiderHeroId, out var raiderHero));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(raiderMobilePartyId, out var raiderParty));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(target.SettlementId, out var settlement));
            Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(defenderTroopId, out var defenderTroop));

            using (new AllowedThread())
            {
                raiderParty.MemberRoster.AddToCounts(raiderHero.CharacterObject, 1);
                raiderHero.PartyBelongedTo = raiderParty;
            }

            var mapEvent = CreateHostileActionMapEvent(raiderParty.Party, settlement.Party, action);
            Assert.NotNull(mapEvent);
            mapEvent.MapEventVisual = MockMapEventVisual();

            var defenderParty = GameObjectCreator.CreateInitializedObject<MobileParty>();
            defenderParty.MemberRoster.AddToCounts(defenderTroop, 1);
            AddSyntheticMapEventParty(mapEvent.DefenderSide, defenderParty.Party);

            Assert.True(mapEvent.IsVillageHostileAction());
            Assert.False(mapEvent.IsVillageHostileActionWithMultiplePlayerParties());
            Assert.True(Server.ObjectManager.TryGetId(mapEvent, out mapEventId));
        }, MapEventDisabledMethods);

        Assert.NotNull(mapEventId);
        return new RaidMapEventContext(mapEventId!, raiderPartyId, target.OwnerFactionId);
    }

    private RaidMapEventContext CreateHostileActionWithTwoPlayerParties(VillageHostileAction action)
    {
        var (raiderHeroId, raiderMobilePartyId) = CreatePlayerHeroParty("PlayerOne");
        var (joinerHeroId, joinerMobilePartyId) = CreatePlayerHeroParty("PlayerTwo");
        var target = CreateVillageTarget();
        var raiderPartyId = GetPartyBaseId(raiderMobilePartyId);
        string? mapEventId = null;

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(raiderHeroId, out var raiderHero));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(joinerHeroId, out var joinerHero));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(raiderMobilePartyId, out var raiderParty));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(joinerMobilePartyId, out var joinerParty));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(target.SettlementId, out var settlement));

            using (new AllowedThread())
            {
                raiderParty.MemberRoster.AddToCounts(raiderHero.CharacterObject, 1);
                joinerParty.MemberRoster.AddToCounts(joinerHero.CharacterObject, 1);
                raiderHero.PartyBelongedTo = raiderParty;
                joinerHero.PartyBelongedTo = joinerParty;
            }

            var mapEvent = CreateHostileActionMapEvent(raiderParty.Party, settlement.Party, action);
            Assert.NotNull(mapEvent);
            mapEvent.MapEventVisual = MockMapEventVisual();
            AddSyntheticMapEventParty(mapEvent.AttackerSide, joinerParty.Party);

            Assert.True(mapEvent.IsVillageHostileActionWithMultiplePlayerParties());
            Assert.True(Server.ObjectManager.TryGetId(mapEvent, out mapEventId));
        }, MapEventDisabledMethods);

        Assert.NotNull(mapEventId);
        return new RaidMapEventContext(mapEventId!, raiderPartyId, target.OwnerFactionId);
    }

    private static BattleCreationFlags RaidFlags() => HostileActionFlags(VillageHostileAction.Raid);

    private static BattleCreationFlags HostileActionFlags(VillageHostileAction action) => new BattleCreationFlags(
        forceRaid: action == VillageHostileAction.Raid,
        forceSallyOut: false,
        forceVolunteers: action == VillageHostileAction.ForceVolunteers,
        forceSupplies: action == VillageHostileAction.ForceSupplies,
        isSallyOutAmbush: false,
        forceBlockadeAttack: false,
        forceBlockadeSallyOutAttack: false,
        forceHideoutSendTroops: false);

    private readonly record struct VillageTarget(string SettlementId, string VillageId, string SettlementPartyId, string OwnerFactionId);
    private readonly record struct RaidMapEventContext(string MapEventId, string AttackerPartyId, string OwnerFactionId);
}
