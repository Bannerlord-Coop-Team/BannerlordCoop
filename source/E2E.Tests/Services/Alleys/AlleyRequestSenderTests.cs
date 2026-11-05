using Common.Messaging;
using Common.Network;
using Common.Util;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.Alleys.Messages;
using GameInterface.Services.Alleys.Patches;
using GameInterface.Services.TroopRosters.Data;
using Moq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Alleys;

/// <summary>The server only applies an alley request from the player whose hero it is about.</summary>
public class AlleyRequestSenderTests : AlleyTestEnvironment
{
    public AlleyRequestSenderTests(ITestOutputHelper output) : base(output, numClients: 2)
    {
    }

    private EnvironmentInstance OwnerClient => Clients.First();
    private EnvironmentInstance OtherClient => Clients.Last();

    [Fact]
    public void AcquireForAnotherPlayersHero_IsRefused()
    {
        var scenario = CreateAlleyDomainScenario();

        AcquireAlley(OtherClient, scenario, scenario.OwnerHeroId);

        Server.Call(() => Assert.Null(Server.GetRegisteredObject<Alley>(scenario.PlayerAlleyId).Owner));
    }

    [Fact]
    public void ManagingAnotherPlayersAlley_IsRefused()
    {
        var scenario = AcquiredByOwner();
        string troopId = CreateRegisteredObject<CharacterObject>();

        OtherClient.Call(() =>
        {
            var alley = OtherClient.GetRegisteredObject<Alley>(scenario.PlayerAlleyId);
            var roster = TroopRoster.CreateDummyTroopRoster();
            roster.AddToCounts(OtherClient.GetRegisteredObject<CharacterObject>(troopId), 7);
            var broker = OtherClient.Resolve<IMessageBroker>();
            broker.Publish(this, new ChangeAlleyOverseerRequested(alley, OtherClient.GetRegisteredObject<Hero>(scenario.OverseerHeroId)));
            broker.Publish(this, new SetAlleyGarrisonRequested(alley, roster));
            broker.Publish(this, new AbandonAlleyRequested(alley, fromClanScreen: true));
        });

        AssertOwnedByOwner(scenario);
        var management = GetManagementData(scenario.PlayerAlleyId);
        Assert.Equal(scenario.OwnerHeroId, management.OverseerId);
        Assert.DoesNotContain(management.Garrison, element => element.CharacterId == Server.GetHandle<CharacterObject>(troopId));
    }

    [Fact]
    public void ResolveDefenseOfAnotherPlayersAlley_IsRefused()
    {
        var scenario = AcquiredByOwner();
        ForceAttack(scenario);

        ResolveDefense(OtherClient, scenario, won: false);

        AssertOwnedByOwner(scenario);
        Assert.Equal(scenario.AttackerAlleyId, GetManagementData(scenario.PlayerAlleyId).UnderAttackByAlleyId);
    }

    [Fact]
    public void TakeOverRefusedAtDone_LeavesTheTroopsWithTheParty()
    {
        var scenario = CreateAlleyDomainScenario();
        string troopId = CreateRegisteredObject<CharacterObject>();
        foreach (var instance in Clients.Prepend(Server))
        {
            instance.Call(() =>
            {
                var troop = instance.GetRegisteredObject<CharacterObject>(troopId);
                troop.UpgradeTargets = Array.Empty<CharacterObject>();
                using (new AllowedThread())
                    instance.GetRegisteredObject<MobileParty>(scenario.OwnerPartyId).MemberRoster.AddToCounts(troop, 10);
            });
        }
        TakenByOther(scenario);

        // The other take over had not reached the owner yet, so it still saw the alley empty when it pressed Done.
        OwnerClient.Call(() =>
        {
            var alley = OwnerClient.GetRegisteredObject<Alley>(scenario.PlayerAlleyId);
            var owner = OwnerClient.GetRegisteredObject<Hero>(scenario.OwnerHeroId);
            var party = OwnerClient.GetRegisteredObject<MobileParty>(scenario.OwnerPartyId);
            var garrison = TroopRoster.CreateDummyTroopRoster();
            garrison.AddToCounts(owner.CharacterObject, 1);
            var logic = new PartyScreenLogic();
            logic.Initialize(new PartyScreenLogicInitializationData
            {
                LeftMemberRoster = garrison,
                LeftPrisonerRoster = TroopRoster.CreateDummyTroopRoster(),
                RightMemberRoster = party.MemberRoster,
                RightPrisonerRoster = party.PrisonRoster,
                RightOwnerParty = party.Party,
                PartyPresentationDoneButtonDelegate = (left, _, _, _, _, _, _, _, _) =>
                {
                    MessageBroker.Instance.Publish(this, new AlleyAcquiredRequested(alley, owner, owner, left, previousOwner: null));
                    return true;
                },
            });
            var command = new PartyScreenLogic.PartyCommand();
            command.FillForTransferTroop(PartyScreenLogic.PartyRosterSide.Right, PartyScreenLogic.TroopType.Member,
                OwnerClient.GetRegisteredObject<CharacterObject>(troopId), 10, 0, -1);
            using (new AllowedThread()) logic.TransferTroop(command, false);
            Assert.True(logic.DoneLogic(false));
        });

        AssertOwnedByOther(scenario);
        Server.Call(() => Assert.Equal(10, Server.GetRegisteredObject<MobileParty>(scenario.OwnerPartyId).MemberRoster
            .GetTroopCount(Server.GetRegisteredObject<CharacterObject>(troopId))));
    }

    [Fact]
    public void TakeOverThatReachedTheTroopScreen_IsRefused()
    {
        var scenario = CreateAlleyDomainScenario();
        var mission = new Mock<ICampaignMission>();
        OwnerClient.Call(() => mission.SetupGet(value => value.LastVisitedAlley)
            .Returns(OwnerClient.GetRegisteredObject<Alley>(scenario.PlayerAlleyId)));
        OwnerClient.CampaignMissionContext = mission.Object;
        try
        {
            OwnerClient.Call(() => MissionAlleyHandlerPatches.OnAlleyFightEndPrefix(isPlayerSideWon: true));
            TakenByOther(scenario);

            OwnerClient.Call(() =>
            {
                var alley = OwnerClient.GetRegisteredObject<Alley>(scenario.PlayerAlleyId);
                Assert.Same(OwnerClient.GetRegisteredObject<Hero>(scenario.OverseerHeroId), alley.Owner);
                var garrison = TroopRoster.CreateDummyTroopRoster();
                garrison.AddToCounts(OwnerClient.GetRegisteredObject<Hero>(scenario.OwnerHeroId).CharacterObject, 1);
                MissionAlleyHandlerPatches.OnPartyScreenDoneClickedPostfix(garrison);
            });
        }
        finally
        {
            OwnerClient.CampaignMissionContext = null;
        }

        AssertOwnedByOther(scenario);
    }

    [Fact]
    public void SecondAcquireByTheOwner_IsRefused()
    {
        var scenario = AcquiredByOwner();

        AcquireAlley(OwnerClient, scenario, scenario.OverseerHeroId);

        AssertOwnedByOwner(scenario);
        Assert.Equal(scenario.OwnerHeroId, GetManagementData(scenario.PlayerAlleyId).OverseerId);
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
