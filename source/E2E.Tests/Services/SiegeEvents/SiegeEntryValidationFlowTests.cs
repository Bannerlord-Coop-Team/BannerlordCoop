using Common;
using Common.Network;
using Common.Util;
using Coop.Core.Client.Services.MobileParties.Messages;
using Coop.Core.Client.Services.SiegeEvents.Handlers;
using Coop.Core.Client.Services.SiegeEvents.Messages;
using Coop.Core.Server.Services.MobileParties.Messages;
using Coop.Core.Server.Services.SiegeEvents.Messages;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Services.MapEvents;
using E2E.Tests.Util;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.SiegeEvents.Interfaces;
using GameInterface.Services.Villages.Interfaces;
using HarmonyLib;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.SiegeEvents;

public class SiegeEntryValidationFlowTests : MapEventTestBase
{
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
        AccessTools.Method(
            typeof(ChangeRelationAction),
            "ApplyInternal"),
    };

    public SiegeEntryValidationFlowTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void SettlementEncounterRequest_WhenPartyIsFarFromSettlement_IsRejected()
    {
        var client = Clients.First();
        var context = CreateEntryContext(client);
        var farPosition = Position(900f, 900f);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(
                context.PartyId,
                out var party));
            using (new AllowedThread())
            {
                party.Position = farPosition;
            }
        });

        client.Call(() => client.Resolve<INetwork>().SendAll(
            new NetworkRequestStartSettlementEncounter(
                context.PartyId,
                context.SettlementId)));

        Assert.Single(
            Server.NetworkSentMessages.GetMessages<NetworkSettlementEncounterRejected>());
        AssertInformationMessage(
            client,
            "Unable to enter the settlement: your party is too far from the settlement.");
        Assert.Empty(
            Server.NetworkSentMessages.GetMessages<NetworkStartSettlementEncounter>());
        Assert.Empty(
            Server.NetworkSentMessages.GetMessages<NetworkPartyEnterSettlement>());
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(
                context.PartyId,
                out var party));
            Assert.Null(party.CurrentSettlement);
            Assert.True(party.Position.Distance(farPosition) < 0.001f);
        });

        client.InternalMessages.Clear();
        SendBesiegeRequest(client, context);

        Assert.False(GetBesiegeApproval().Approved);
        AssertInformationMessage(
            client,
            "Unable to begin the siege: your party is too far from the settlement.");
        AssertNoSiege(context);
    }

    [Fact]
    public void SettlementEncounterRequest_ForPartyControlledByAnotherPeer_IsRejected()
    {
        var owner = Clients.First();
        var requester = Clients.Skip(1).First();
        var context = CreateEntryContext(owner);
        ConnectAdditionalPlayer(requester, "PlayerTwo");

        requester.Call(() => requester.Resolve<INetwork>().SendAll(
            new NetworkRequestStartSettlementEncounter(
                context.PartyId,
                context.SettlementId)));

        Assert.Single(
            Server.NetworkSentMessages.GetMessages<NetworkSettlementEncounterRejected>());
        AssertInformationMessage(
            requester,
            "Unable to enter the settlement: your party is not controlled by you.");
        Assert.Empty(
            Server.NetworkSentMessages.GetMessages<NetworkStartSettlementEncounter>());
        Assert.Empty(
            Server.NetworkSentMessages.GetMessages<NetworkPartyEnterSettlement>());
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(
                context.PartyId,
                out var party));
            Assert.Null(party.CurrentSettlement);
        });
    }

    [Fact]
    public void EndSettlementEncounterRequest_ForPartyControlledByAnotherPeer_IsRejected()
    {
        var owner = Clients.First();
        var requester = Clients.Skip(1).First();
        var context = CreateEntryContext(owner);
        ConnectAdditionalPlayer(requester, "PlayerTwo");

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
        });
        ClearMessages();

        requester.Call(() => requester.Resolve<INetwork>().SendAll(
            new NetworkRequestEndSettlementEncounter(context.PartyId)));

        var result = Assert.Single(
            Server.NetworkSentMessages.GetMessages<NetworkSettlementEncounterLeaveResult>());
        Assert.Equal(SettlementEncounterLeaveOutcome.Suppressed, result.Outcome);
        AssertInformationMessage(
            requester,
            "Unable to leave the settlement: your party is not controlled by you.");
        Assert.Empty(
            Server.NetworkSentMessages.GetMessages<NetworkPartyLeaveSettlement>());
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(
                context.PartyId,
                out var party));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(
                context.SettlementId,
                out var settlement));
            Assert.Same(settlement, party.CurrentSettlement);
        });
    }

    [Fact]
    public void BesiegeRequest_WhenPartyIsFarFromSettlement_IsRejected()
    {
        var client = Clients.First();
        var context = CreateEntryContext(client);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(
                context.PartyId,
                out var party));
            using (new AllowedThread())
            {
                party.Position = Position(900f, 900f);
            }
        });

        SendBesiegeRequest(client, context);

        Assert.False(GetBesiegeApproval().Approved);
        AssertInformationMessage(
            client,
            "Unable to begin the siege: your party is too far from the settlement.");
        AssertNoSiege(context);
    }

    [Fact]
    public void BesiegeRequest_ForOwnSettlement_IsRejected()
    {
        var client = Clients.First();
        var context = CreateEntryContext(client);

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

        SendBesiegeRequest(client, context);

        Assert.False(GetBesiegeApproval().Approved);
        AssertInformationMessage(
            client,
            "Unable to begin the siege: your party belongs to the defending faction.");
        AssertNoSiege(context);
    }

    [Fact]
    public void BesiegeRequest_WithValidServerState_IsApproved()
    {
        var client = Clients.First();
        var context = CreateEntryContext(client);
        IgnoreEntryResults(client);

        SendBesiegeRequest(client, context, SiegeCreationDisabledMethods);

        Assert.True(GetBesiegeApproval().Approved);
        AssertSiegeStarted(context);
    }

    [Fact]
    public void BesiegeRequest_WhenPartyAlreadyBesiegesTarget_IsApproved()
    {
        var client = Clients.First();
        var context = CreateEntryContext(client);
        IgnoreEntryResults(client);

        SendBesiegeRequest(client, context, SiegeCreationDisabledMethods);

        SiegeEvent? originalSiege = null;
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(
                context.SettlementId,
                out var settlement));
            originalSiege = settlement.SiegeEvent;
            Assert.NotNull(originalSiege);
        });
        ClearMessages();

        SendBesiegeRequest(client, context);

        Assert.True(GetBesiegeApproval().Approved);
        Assert.Empty(client.InternalMessages.GetMessages<SendInformationMessage>());
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(
                context.PartyId,
                out var party));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(
                context.SettlementId,
                out var settlement));
            var currentSiege = settlement.SiegeEvent;
            Assert.NotNull(currentSiege);
            Assert.Same(originalSiege, currentSiege);
            Assert.Same(currentSiege.BesiegerCamp, party.BesiegerCamp);
        });
    }

    [Fact]
    public void JoinRequest_WithValidServerState_IsApproved()
    {
        var client = Clients.First();
        var context = CreateEntryContext(client);

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
                CreatePresentedSiege(party, settlement);
            }

            Assert.True(settlement.SiegeEvent.CanPartyJoinSide(
                party.Party,
                BattleSideEnum.Attacker));
        }, SiegeCreationDisabledMethods);
        Server.NetworkSentMessages.Clear();
        IgnoreEntryResults(client);

        SendJoinRequest(client, context, SiegeCreationDisabledMethods);

        var approval = GetJoinApproval();
        Assert.True(approval.Approved);
        Assert.Equal(context.SettlementId, approval.SettlementId);
        AssertJoinedSiege(context);
    }

    [Fact]
    public void JoinRequest_WhenPartyIsFarFromSettlement_IsRejected()
    {
        var client = Clients.First();
        var context = CreateJoinContext(client);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(
                context.PartyId,
                out var party));
            using (new AllowedThread())
            {
                party.Position = Position(900f, 900f);
            }
        });

        SendJoinRequest(client, context);

        Assert.False(GetJoinApproval().Approved);
        AssertInformationMessage(
            client,
            "Unable to join the siege: your party is too far from the settlement.");
        AssertNotJoined(context);
    }

    [Fact]
    public void JoinRequest_WhenPartyBelongsToDefender_IsRejected()
    {
        var client = Clients.First();
        var context = CreateJoinContext(client);

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

        SendJoinRequest(client, context);

        Assert.False(GetJoinApproval().Approved);
        AssertInformationMessage(
            client,
            "Unable to join the siege: your party belongs to the defending faction.");
        AssertNotJoined(context);
    }

    private EntryContext CreateEntryContext(EnvironmentInstance client)
    {
        const string controllerId = "PlayerOne";
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
                party.Position = settlement.GatePosition;
                party.IsActive = true;
            }

            if (party.Party.NumberOfHealthyMembers == 0)
                party.MemberRoster.AddToCounts(party.LeaderHero.CharacterObject, 1);

            VillageHostileFactionStanceHelper.ApplyWarStance(
                party.ActualClan,
                defenderClan);
        });

        Server.NetworkSentMessages.Clear();
        client.NetworkSentMessages.Clear();
        foreach (var connectedClient in Clients)
            connectedClient.InternalMessages.Clear();
        return new EntryContext(partyId, settlementId, townId);
    }

    private EntryContext CreateJoinContext(EnvironmentInstance client)
    {
        var context = CreateEntryContext(client);
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
                CreatePresentedSiege(party, settlement);
            }
        }, SiegeCreationDisabledMethods);
        Server.NetworkSentMessages.Clear();
        return context;
    }

    private void ConnectAdditionalPlayer(EnvironmentInstance client, string controllerId)
    {
        CreatePlayerHeroParty(controllerId);
        TestEnvironment.ConnectRegisteredPlayer(client, controllerId);
        ClearMessages();
    }

    private void ClearMessages()
    {
        Server.NetworkSentMessages.Clear();
        foreach (var client in Clients)
        {
            client.NetworkSentMessages.Clear();
            client.InternalMessages.Clear();
        }
    }

    private void SendBesiegeRequest(
        EnvironmentInstance client,
        EntryContext context,
        IEnumerable<MethodBase>? disabledMethods = null)
    {
        client.Call(
            () => client.Resolve<INetwork>().SendAll(
                new NetworkRequestBesiegeSettlement(
                    context.PartyId,
                    context.SettlementId)),
            disabledMethods);
    }

    private static void SendJoinRequest(
        EnvironmentInstance client,
        EntryContext context,
        IEnumerable<MethodBase>? disabledMethods = null)
    {
        client.Call(
            () => client.Resolve<INetwork>().SendAll(
                new NetworkRequestJoinSiegeCamp(
                    context.PartyId,
                    context.SettlementId)),
            disabledMethods);
    }

    private static void IgnoreEntryResults(EnvironmentInstance client) =>
        client.Resolve<ClientSiegeEntryHandler>().Dispose();

    private NetworkBesiegeSettlementApproved GetBesiegeApproval() =>
        Assert.Single(
            Server.NetworkSentMessages.GetMessages<NetworkBesiegeSettlementApproved>());

    private NetworkJoinSiegeCampApproved GetJoinApproval() =>
        Assert.Single(
            Server.NetworkSentMessages.GetMessages<NetworkJoinSiegeCampApproved>());

    private void AssertInformationMessage(
        EnvironmentInstance client,
        string expectedText)
    {
        var message = Assert.Single(
            client.InternalMessages.GetMessages<SendInformationMessage>());
        Assert.Equal(expectedText, message.Text);

        foreach (var otherClient in Clients.Where(otherClient => otherClient != client))
            Assert.Empty(otherClient.InternalMessages.GetMessages<SendInformationMessage>());
    }

    private void AssertNoSiege(EntryContext context)
    {
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(
                context.SettlementId,
                out var settlement));
            Assert.Null(settlement.SiegeEvent);
        });
    }

    private void AssertSiegeStarted(EntryContext context)
    {
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(
                context.PartyId,
                out var party));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(
                context.SettlementId,
                out var settlement));
            Assert.NotNull(settlement.SiegeEvent);
            Assert.Same(settlement.SiegeEvent.BesiegerCamp, party.BesiegerCamp);
        });
    }

    private void AssertJoinedSiege(EntryContext context)
    {
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(
                context.PartyId,
                out var party));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(
                context.SettlementId,
                out var settlement));
            Assert.Same(settlement.SiegeEvent.BesiegerCamp, party.BesiegerCamp);
        });
    }

    private void AssertNotJoined(EntryContext context)
    {
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(
                context.PartyId,
                out var party));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(
                context.SettlementId,
                out var settlement));
            Assert.NotNull(settlement.SiegeEvent);
            Assert.Null(party.BesiegerCamp);
        });
    }

    private static void CreatePresentedSiege(
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
    }

    private static CampaignVec2 Position(float x, float y) =>
        new CampaignVec2(new Vec2(x, y), isOnLand: true);

    private sealed record EntryContext(
        string PartyId,
        string SettlementId,
        string TownId);
}
