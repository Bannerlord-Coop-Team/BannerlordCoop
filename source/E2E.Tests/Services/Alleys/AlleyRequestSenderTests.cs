using Common.Messaging;
using Common.Network;
using Common.Util;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.Alleys.Messages;
using GameInterface.Services.Alleys.Patches;
using GameInterface.Services.TroopRosters.Data;
using HarmonyLib;
using Helpers;
using Moq;
using SandBox.Missions.MissionLogics;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Alleys;

/// <summary>Alley take overs and management requests from two players, applied on the server.</summary>
public class AlleyRequestSenderTests : AlleyTestEnvironment
{
    public AlleyRequestSenderTests(ITestOutputHelper output) : base(output, numClients: 2)
    {
    }

    private EnvironmentInstance OwnerClient => Clients.First();
    private EnvironmentInstance OtherClient => Clients.Last();

    [Fact]
    public void TakeOverRacedByAnotherPlayer_ChangesNothingOnAnyPeer()
    {
        var scenario = CreateAlleyDomainScenario();
        var (troopId, companionId) = AddTroopsAndCompanion(scenario);

        // The other take over reaches this client while it is still in the alley fight.
        PressDoneOnTakeOverScreen(scenario, troopId, companionId, duringFight: () => TakenByOther(scenario));

        AssertOwnedByOther(scenario);
        foreach (var instance in Clients.Prepend(Server))
        {
            instance.Call(() =>
            {
                var roster = instance.GetRegisteredObject<MobileParty>(scenario.OwnerPartyId).MemberRoster;
                Assert.Equal(10, roster.GetTroopCount(instance.GetRegisteredObject<CharacterObject>(troopId)));
                Assert.True(roster.Contains(instance.GetRegisteredObject<Hero>(companionId).CharacterObject));
            });
        }
    }

    [Fact]
    public void TakeOver_MovesTheTroopsAndTheirWoundedOnEveryPeer()
    {
        var scenario = CreateAlleyDomainScenario();
        var (troopId, companionId) = AddTroopsAndCompanion(scenario);

        PressDoneOnTakeOverScreen(scenario, troopId, companionId, duringFight: null);

        AssertOwnedByOwner(scenario);
        var management = GetManagementData(scenario.PlayerAlleyId);
        Assert.Equal(companionId, management.OverseerId);
        var stored = management.Garrison.Single(element => element.CharacterId == Server.GetHandle<CharacterObject>(troopId));
        Assert.Equal((6, 2), (stored.Number, stored.WoundedNumber));
        foreach (var instance in Clients.Prepend(Server))
        {
            instance.Call(() =>
            {
                Assert.Same(instance.GetRegisteredObject<Hero>(scenario.OwnerHeroId),
                    instance.GetRegisteredObject<Alley>(scenario.PlayerAlleyId).Owner);
                // The two wounded went to the alley with the troops.
                var roster = instance.GetRegisteredObject<MobileParty>(scenario.OwnerPartyId).MemberRoster;
                int index = roster.FindIndexOfTroop(instance.GetRegisteredObject<CharacterObject>(troopId));
                Assert.Equal((4, 0), (roster.GetElementNumber(index), roster.GetElementWoundedNumber(index)));
            });
        }
    }

    [Fact]
    public void TwoClientsFightBeforeDone_EachSendsItsOwnBeatenOwner()
    {
        var scenario = CreateAlleyDomainScenario();
        var fights = new[] { (OwnerClient, scenario.PlayerAlleyId, scenario.OwnerHeroId), (OtherClient, scenario.AttackerAlleyId, scenario.OverseerHeroId) };
        var sent = new Dictionary<EnvironmentInstance, Hero>();
        try
        {
            foreach (var (client, alleyId, heroId) in fights)
            {
                var mission = new Mock<ICampaignMission>();
                client.Call(() =>
                {
                    mission.SetupGet(value => value.LastVisitedAlley).Returns(client.GetRegisteredObject<Alley>(alleyId));
                    Game.Current.PlayerTroop = client.GetRegisteredObject<Hero>(heroId).CharacterObject;
                    client.Resolve<IMessageBroker>().Subscribe<AlleyAcquiredRequested>(payload => sent[client] = payload.What.PreviousOwner);
                });
                client.CampaignMissionContext = mission.Object;
                StartFight(client, alleyId);
            }

            foreach (var (client, _, heroId) in fights)
            {
                client.Call(() =>
                {
                    var left = TroopRoster.CreateDummyTroopRoster();
                    left.AddToCounts(client.GetRegisteredObject<Hero>(heroId).CharacterObject, 1);
                    new MissionAlleyHandler().OnPartyScreenDoneClicked(left, null, TroopRoster.CreateDummyTroopRoster(), null, null, null, false, null, null);
                });
            }
        }
        finally
        {
            foreach (var (client, _, _) in fights) client.CampaignMissionContext = null;
        }

        Assert.Null(sent[OwnerClient]);
        OtherClient.Call(() => Assert.Same(OtherClient.GetRegisteredObject<Hero>(scenario.GangLeaderId), sent[OtherClient]));
    }

    // Ten troops, two of them wounded, and a clan member in the owner's party on every peer.
    private (string troopId, string companionId) AddTroopsAndCompanion(AlleyDomainScenario scenario)
    {
        string troopId = CreateRegisteredObject<CharacterObject>();
        var (companionId, _) = CreateHeroCharacter();
        foreach (var instance in Clients.Prepend(Server))
        {
            instance.Call(() =>
            {
                instance.GetRegisteredObject<CharacterObject>(troopId).UpgradeTargets = Array.Empty<CharacterObject>();
                instance.GetRegisteredObject<Hero>(companionId).Clan = instance.GetRegisteredObject<Hero>(scenario.OwnerHeroId).Clan;
            });
        }
        // The server's roster changes reach both clients.
        Server.Call(() =>
        {
            var roster = Server.GetRegisteredObject<MobileParty>(scenario.OwnerPartyId).MemberRoster;
            roster.AddToCounts(Server.GetRegisteredObject<CharacterObject>(troopId), 10, false, 2);
            roster.AddToCounts(Server.GetRegisteredObject<Hero>(companionId).CharacterObject, 1);
        });
        return (troopId, companionId);
    }

    // Starts and wins the alley fight, moves six troops, two wounded, on the take over screen the way vanilla opens it, then presses Done.
    private void PressDoneOnTakeOverScreen(AlleyDomainScenario scenario, string troopId, string companionId, Action duringFight)
    {
        var mission = new Mock<ICampaignMission>();
        OwnerClient.Call(() => mission.SetupGet(value => value.LastVisitedAlley)
            .Returns(OwnerClient.GetRegisteredObject<Alley>(scenario.PlayerAlleyId)));
        OwnerClient.CampaignMissionContext = mission.Object;
        var handler = new MissionAlleyHandler();
        try
        {
            StartFight(OwnerClient, scenario.PlayerAlleyId);
            duringFight?.Invoke();

            PartyScreenLogic logic = null;
            OwnerClient.Call(() =>
            {
                var party = OwnerClient.GetRegisteredObject<MobileParty>(scenario.OwnerPartyId);
                Campaign.Current.MainParty = party;
                handler.OnAlleyFightEnd(isPlayerSideWon: true);

                var companion = OwnerClient.GetRegisteredObject<Hero>(companionId).CharacterObject;
                var left = TroopRoster.CreateDummyTroopRoster();
                left.AddToCounts(companion, 1);
                var right = party.MemberRoster.CloneRosterData();
                right.RemoveTroop(companion);
                logic = new PartyScreenLogic();
                logic.Initialize(new PartyScreenLogicInitializationData
                {
                    LeftMemberRoster = left,
                    LeftPrisonerRoster = TroopRoster.CreateDummyTroopRoster(),
                    RightMemberRoster = right,
                    RightPrisonerRoster = TroopRoster.CreateDummyTroopRoster(),
                    RightOwnerParty = party.Party,
                    PartyScreenMode = PartyScreenHelper.PartyScreenMode.TroopsManage,
                    PartyPresentationDoneButtonDelegate = handler.OnPartyScreenDoneClicked,
                });
                var command = new PartyScreenLogic.PartyCommand();
                command.FillForTransferTroop(PartyScreenLogic.PartyRosterSide.Right, PartyScreenLogic.TroopType.Member,
                    OwnerClient.GetRegisteredObject<CharacterObject>(troopId), 6, 2, -1);
                using (new AllowedThread()) logic.TransferTroop(command, false);
            });

            OwnerClient.Call(() => Assert.True(logic.DoneLogic(false)));
        }
        finally
        {
            OwnerClient.CampaignMissionContext = null;
        }
    }

    [Fact]
    public void OverseerChangePublishedOnServer_Applies()
    {
        var scenario = AcquiredByOwner();

        // ClanLeaveRules publishes this on the server with no peer when an overseer leaves the clan.
        Server.Call(() => Server.Resolve<IMessageBroker>().Publish(
            this, new RequestChangeAlleyOverseer(scenario.PlayerAlleyId, scenario.OverseerHeroId)));

        Assert.Equal(scenario.OverseerHeroId, GetManagementData(scenario.PlayerAlleyId).OverseerId);
    }

    // Runs the prefix vanilla StartCommonAreaBattle runs first. Its own body needs a live mission with agents.
    private static void StartFight(EnvironmentInstance client, string alleyId)
    {
        var prefix = AccessTools.Method(typeof(MissionAlleyHandlerPatches), "StartCommonAreaBattlePrefix");
        client.Call(() => prefix.Invoke(null, new object[] { client.GetRegisteredObject<Alley>(alleyId) }));
    }

    // The other player wins the same alley and takes it over first.
    private void TakenByOther(AlleyDomainScenario scenario)
    {
        OtherClient.Call(() => OtherClient.Resolve<INetwork>().SendAll(new RequestAcquireAlley(
            scenario.PlayerAlleyId, scenario.OverseerHeroId, scenario.OverseerHeroId,
            Array.Empty<TroopRosterElementData>(), previousOwnerId: null)));
        AssertOwnedByOther(scenario);
    }

    private void AssertOwnedByOther(AlleyDomainScenario scenario) =>
        Server.Call(() => Assert.Same(Server.GetRegisteredObject<Hero>(scenario.OverseerHeroId),
            Server.GetRegisteredObject<Alley>(scenario.PlayerAlleyId).Owner));

    private AlleyDomainScenario AcquiredByOwner()
    {
        var scenario = CreateAlleyDomainScenario();
        AcquireAlley(OwnerClient, scenario, scenario.OwnerHeroId);
        AssertOwnedByOwner(scenario);
        return scenario;
    }

    private void AssertOwnedByOwner(AlleyDomainScenario scenario) =>
        Server.Call(() => Assert.Same(Server.GetRegisteredObject<Hero>(scenario.OwnerHeroId),
            Server.GetRegisteredObject<Alley>(scenario.PlayerAlleyId).Owner));
}
