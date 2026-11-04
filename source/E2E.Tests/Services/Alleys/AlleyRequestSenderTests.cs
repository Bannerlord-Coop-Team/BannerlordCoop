using Common.Messaging;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.Alleys;
using GameInterface.Services.Alleys.Interfaces;
using GameInterface.Services.Alleys.Messages;
using GameInterface.Services.TroopRosters.Data;
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

        AcquireAlley(OtherClient, scenario, scenario.OwnerHeroId,
            new TroopRosterElementData(CharacterHandle(GetCharacterId(scenario.OwnerHeroId)), 1, 0, 0));

        Server.Call(() =>
        {
            Assert.Null(Server.GetRegisteredObject<Alley>(scenario.PlayerAlleyId).Owner);
            Assert.False(Server.Resolve<ISessionAlleyPlayerDataInterface>()
                .TryGetManagementData(scenario.PlayerAlleyId, out _));
        });
    }

    [Fact]
    public void AbandonAnotherPlayersAlley_IsRefused()
    {
        var scenario = AcquiredByOwner();

        OtherClient.Call(() => OtherClient.Resolve<IMessageBroker>().Publish(this,
            new AbandonAlleyRequested(OtherClient.GetRegisteredObject<Alley>(scenario.PlayerAlleyId), fromClanScreen: true)));

        AssertStillOwned(scenario);
    }

    [Fact]
    public void ChangeOverseerOfAnotherPlayersAlley_IsRefused()
    {
        var scenario = AcquiredByOwner();

        OtherClient.Call(() => OtherClient.Resolve<IMessageBroker>().Publish(this,
            new ChangeAlleyOverseerRequested(
                OtherClient.GetRegisteredObject<Alley>(scenario.PlayerAlleyId),
                OtherClient.GetRegisteredObject<Hero>(scenario.OverseerHeroId))));

        AssertStillOwned(scenario);
        Assert.Equal(scenario.OwnerHeroId, GetManagementData(scenario.PlayerAlleyId).OverseerId);
    }

    [Fact]
    public void SetGarrisonOfAnotherPlayersAlley_IsRefused()
    {
        var scenario = AcquiredByOwner();
        string troopId = CreateRegisteredObject<CharacterObject>();

        OtherClient.Call(() =>
        {
            var roster = TroopRoster.CreateDummyTroopRoster();
            roster.AddToCounts(OtherClient.GetRegisteredObject<CharacterObject>(troopId), 7);
            OtherClient.Resolve<IMessageBroker>().Publish(this,
                new SetAlleyGarrisonRequested(OtherClient.GetRegisteredObject<Alley>(scenario.PlayerAlleyId), roster));
        });

        AssertStillOwned(scenario);
        Assert.DoesNotContain(GetManagementData(scenario.PlayerAlleyId).Garrison,
            element => element.CharacterId == CharacterHandle(troopId));
    }

    [Fact]
    public void RecruitFromAnotherPlayersAlley_IsRefused()
    {
        var scenario = AcquiredByOwner();
        string troopId = CreateRegisteredObject<CharacterObject>();
        Server.Call(() =>
        {
            Server.Resolve<ISessionAlleyPlayerDataInterface>()
                .SetLastRecruitTimeTicks(scenario.PlayerAlleyId, CampaignTime.Now.NumTicks);
            Campaign.Current.MapTimeTracker._deltaTimeInTicks += CampaignTime.Days(8f).NumTicks;
        });

        OtherClient.Call(() =>
        {
            var roster = TroopRoster.CreateDummyTroopRoster();
            roster.AddToCounts(OtherClient.GetRegisteredObject<CharacterObject>(troopId), 4);
            OtherClient.Resolve<IMessageBroker>().Publish(this,
                new RecruitAlleyTroopsRequested(OtherClient.GetRegisteredObject<Alley>(scenario.PlayerAlleyId), roster));
        });

        Server.Call(() => Assert.Equal(0, Server.GetRegisteredObject<MobileParty>(scenario.OwnerPartyId)
            .MemberRoster.GetTroopCount(Server.GetRegisteredObject<CharacterObject>(troopId))));
    }

    [Fact]
    public void ResolveDefenseOfAnotherPlayersAlley_IsRefused()
    {
        var scenario = AcquiredByOwner();
        ForceAttack(scenario);

        ResolveDefense(OtherClient, scenario, won: false);

        AssertStillOwned(scenario);
        Assert.Equal(scenario.AttackerAlleyId, GetManagementData(scenario.PlayerAlleyId).UnderAttackByAlleyId);
    }

    private AlleyDomainScenario AcquiredByOwner()
    {
        var scenario = CreateAlleyDomainScenario();
        AcquireAlley(OwnerClient, scenario, scenario.OwnerHeroId,
            new TroopRosterElementData(CharacterHandle(GetCharacterId(scenario.OwnerHeroId)), 1, 0, 0));
        AssertStillOwned(scenario);
        return scenario;
    }

    private void AssertStillOwned(AlleyDomainScenario scenario)
    {
        Server.Call(() => Assert.Equal(scenario.OwnerHeroId,
            GetId(Server, Server.GetRegisteredObject<Alley>(scenario.PlayerAlleyId).Owner)));
        Assert.NotNull(GetManagementData(scenario.PlayerAlleyId));
    }
    private string GetCharacterId(string heroId)
    {
        string characterId = null;
        Server.Call(() =>
        {
            Hero hero = Server.GetRegisteredObject<Hero>(heroId);
            Assert.True(Server.ObjectManager.TryGetId(hero.CharacterObject, out characterId));
        });
        return characterId;
    }

    private uint CharacterHandle(string characterId) =>
        Server.GetHandle<CharacterObject>(characterId);

    private static string GetId<T>(EnvironmentInstance instance, T value) where T : class
    {
        Assert.NotNull(value);
        Assert.True(instance.ObjectManager.TryGetId(value, out var id));
        return id;
    }
}
