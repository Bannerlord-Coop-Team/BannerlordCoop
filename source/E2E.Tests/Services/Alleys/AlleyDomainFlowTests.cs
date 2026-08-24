using Common.Messaging;
using GameInterface.Services.Alleys;
using GameInterface.Services.Alleys.Interfaces;
using GameInterface.Services.Alleys.Messages;
using GameInterface.Services.TroopRosters.Data;
using SandBox.CampaignBehaviors;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Alleys;

/// <summary>Exercises authoritative alley domain flows through production messages and actions.</summary>
public class AlleyDomainFlowTests : AlleyTestEnvironment
{
    public AlleyDomainFlowTests(ITestOutputHelper output) : base(output, numClients: 2)
    {
    }

    [Fact]
    public void LoadedSessionSeed_RestoresReconnectManagementAndPendingAttack()
    {
        var scenario = CreateAlleyDomainScenario();
        var ownerClient = Clients.First();

        Server.Call(() => Server.GetRegisteredObject<Alley>(scenario.PlayerAlleyId)
            .SetOwner(Server.GetRegisteredObject<Hero>(scenario.OwnerHeroId)));

        SeedLoadedAlleys();

        Server.Call(() =>
        {
            var session = Server.Resolve<ISessionAlleyPlayerDataInterface>();
            Assert.True(session.TryGetManagementData(scenario.PlayerAlleyId, out var seeded));
            Assert.Equal(scenario.OwnerHeroId, seeded.OverseerId);
            Assert.Empty(seeded.Garrison);

            seeded.LastRecruitTimeTicks = CampaignTime.Now.NumTicks - CampaignTime.Days(9f).NumTicks;
            session.SetUnderAttackByAi(
                scenario.PlayerAlleyId,
                scenario.AttackerAlleyId,
                CampaignTime.DaysFromNow(2f));
        });

        RestoreClientAlleyData(ownerClient, scenario.OwnerHeroId);
        RestoreClientAlleyData(ownerClient, scenario.OwnerHeroId);

        ownerClient.Call(() =>
        {
            Alley alley = ownerClient.GetRegisteredObject<Alley>(scenario.PlayerAlleyId);
            Alley attacker = ownerClient.GetRegisteredObject<Alley>(scenario.AttackerAlleyId);
            var entries = Campaign.Current.GetCampaignBehavior<AlleyCampaignBehavior>()
                ._playerOwnedCommonAreaData.Where(data => data.Alley == alley).ToArray();

            var restored = Assert.Single(entries);
            Assert.Equal(Alley.AreaState.OccupiedByPlayer, alley.State);
            Assert.Equal(scenario.OwnerHeroId,
                GetId(ownerClient, restored.AssignedClanMember));
            Assert.Same(attacker, restored.UnderAttackBy);
            Assert.True(restored.LastRecruitTime.ElapsedDaysUntilNow > CampaignTime.DaysInWeek);
        });
    }

    [Fact]
    public void AcquireManageAndAbandon_ReplicatesAuthoritativeManagementState()
    {
        var scenario = CreateAlleyDomainScenario();
        var ownerClient = Clients.First();
        string troopId = CreateRegisteredObject<CharacterObject>();
        string ownerCharacterId = GetCharacterId(scenario.OwnerHeroId);
        string overseerCharacterId = GetCharacterId(scenario.OverseerHeroId);

        AcquireAlley(
            ownerClient,
            scenario,
            scenario.OwnerHeroId,
            new TroopRosterElementData(ownerCharacterId, 1, 0, 0),
            new TroopRosterElementData(troopId, 3, 0, 20));

        ownerClient.Call(() =>
        {
            Alley alley = ownerClient.GetRegisteredObject<Alley>(scenario.PlayerAlleyId);
            Hero overseer = ownerClient.GetRegisteredObject<Hero>(scenario.OverseerHeroId);
            ownerClient.Resolve<IMessageBroker>().Publish(
                this,
                new ChangeAlleyOverseerRequested(alley, overseer));

            var roster = TroopRoster.CreateDummyTroopRoster();
            roster.AddToCounts(overseer.CharacterObject, 1);
            roster.AddToCounts(ownerClient.GetRegisteredObject<CharacterObject>(troopId), 5, xpChange: 35);
            ownerClient.Resolve<IMessageBroker>().Publish(
                this,
                new SetAlleyGarrisonRequested(alley, roster));
        });

        AlleyManagementData managed = GetManagementData(scenario.PlayerAlleyId);
        Assert.Equal(scenario.OverseerHeroId, managed.OverseerId);
        Assert.Contains(managed.Garrison,
            element => element.CharacterId == overseerCharacterId && element.Number == 1);
        Assert.Contains(managed.Garrison,
            element => element.CharacterId == troopId && element.Number == 5 && element.Xp == 35);

        var clientData = GetClientAlleyData(ownerClient, scenario.PlayerAlleyId);
        Assert.Equal(scenario.OverseerHeroId, GetId(ownerClient, clientData.AssignedClanMember));
        Assert.Equal(5, clientData.TroopRoster.GetTroopCount(
            ownerClient.GetRegisteredObject<CharacterObject>(troopId)));

        ownerClient.Call(() => ownerClient.Resolve<IMessageBroker>().Publish(
            this,
            new AbandonAlleyRequested(
                ownerClient.GetRegisteredObject<Alley>(scenario.PlayerAlleyId),
                fromClanScreen: true)));

        Server.Call(() =>
        {
            Assert.Null(Server.GetRegisteredObject<Alley>(scenario.PlayerAlleyId).Owner);
            Assert.False(Server.Resolve<ISessionAlleyPlayerDataInterface>()
                .TryGetManagementData(scenario.PlayerAlleyId, out _));
        });
        ownerClient.Call(() => Assert.DoesNotContain(
            Campaign.Current.GetCampaignBehavior<AlleyCampaignBehavior>()._playerOwnedCommonAreaData,
            data => data.Alley == ownerClient.GetRegisteredObject<Alley>(scenario.PlayerAlleyId)));
    }

    [Fact]
    public void RecruitTroopsAfterWeeklyCooldown_AddsThemToOwnerPartyAndAdvancesCooldown()
    {
        var scenario = CreateAlleyDomainScenario();
        var ownerClient = Clients.First();
        string troopId = CreateRegisteredObject<CharacterObject>();
        string ownerCharacterId = GetCharacterId(scenario.OwnerHeroId);

        AcquireAlley(
            ownerClient,
            scenario,
            scenario.OwnerHeroId,
            new TroopRosterElementData(ownerCharacterId, 1, 0, 0));

        long expectedRecruitTime = 0;
        Server.Call(() =>
        {
            var session = Server.Resolve<ISessionAlleyPlayerDataInterface>();
            Assert.True(session.TryGetManagementData(scenario.PlayerAlleyId, out var data));
            data.LastRecruitTimeTicks = CampaignTime.Now.NumTicks;
            Campaign.Current.MapTimeTracker._deltaTimeInTicks += CampaignTime.Days(8f).NumTicks;
            expectedRecruitTime = CampaignTime.Now.NumTicks;
            Assert.True(new CampaignTime(data.LastRecruitTimeTicks).ElapsedDaysUntilNow >
                CampaignTime.DaysInWeek);
        });

        ownerClient.Call(() =>
        {
            var roster = TroopRoster.CreateDummyTroopRoster();
            roster.AddToCounts(ownerClient.GetRegisteredObject<CharacterObject>(troopId), 4);
            ownerClient.Resolve<IMessageBroker>().Publish(
                this,
                new RecruitAlleyTroopsRequested(
                    ownerClient.GetRegisteredObject<Alley>(scenario.PlayerAlleyId),
                    roster));
        });

        Server.Call(() =>
        {
            MobileParty party = Server.GetRegisteredObject<MobileParty>(scenario.OwnerPartyId);
            CharacterObject troop = Server.GetRegisteredObject<CharacterObject>(troopId);
            Assert.Equal(4, party.MemberRoster.GetTroopCount(troop));
            Assert.Equal(expectedRecruitTime, GetManagementData(scenario.PlayerAlleyId).LastRecruitTimeTicks);
        });
    }

    [Fact]
    public void DailySimulation_GrantsOwnerAndOverseerRogueryXp()
    {
        var scenario = CreateAlleyDomainScenario();
        var ownerClient = Clients.First();
        string overseerCharacterId = GetCharacterId(scenario.OverseerHeroId);

        AcquireAlley(
            ownerClient,
            scenario,
            scenario.OverseerHeroId,
            new TroopRosterElementData(overseerCharacterId, 1, 0, 0));

        Server.Call(() =>
        {
            Hero owner = Server.GetRegisteredObject<Hero>(scenario.OwnerHeroId);
            Hero overseer = Server.GetRegisteredObject<Hero>(scenario.OverseerHeroId);
            float ownerXpBefore = owner.HeroDeveloper.GetSkillXp(DefaultSkills.Roguery);
            float overseerXpBefore = overseer.HeroDeveloper.GetSkillXp(DefaultSkills.Roguery);
            Server.Resolve<ISessionAlleyPlayerDataInterface>().SetUnderAttackByAi(
                scenario.PlayerAlleyId,
                scenario.AttackerAlleyId,
                CampaignTime.DaysFromNow(2f));
            Server.Resolve<IMessageBroker>().Publish(this, new AlleyDailyTickTriggered());

            Assert.True(owner.HeroDeveloper.GetSkillXp(DefaultSkills.Roguery) > ownerXpBefore);
            Assert.True(overseer.HeroDeveloper.GetSkillXp(DefaultSkills.Roguery) > overseerXpBefore);
        });
    }

    [Fact]
    public void DailySimulation_ExpiredUnansweredAttackTransfersAlleyAndDropsManagement()
    {
        var scenario = CreateAlleyDomainScenario();
        var ownerClient = Clients.First();
        string ownerCharacterId = GetCharacterId(scenario.OwnerHeroId);

        AcquireAlley(
            ownerClient,
            scenario,
            scenario.OwnerHeroId,
            new TroopRosterElementData(ownerCharacterId, 1, 0, 0));

        Server.Call(() =>
        {
            Server.Resolve<ISessionAlleyPlayerDataInterface>().SetUnderAttackByAi(
                scenario.PlayerAlleyId,
                scenario.AttackerAlleyId,
                CampaignTime.Now - CampaignTime.Days(1f));
            Server.Resolve<IMessageBroker>().Publish(this, new AlleyDailyTickTriggered());

            Alley playerAlley = Server.GetRegisteredObject<Alley>(scenario.PlayerAlleyId);
            Hero gangLeader = Server.GetRegisteredObject<Hero>(scenario.GangLeaderId);
            Assert.Same(gangLeader, playerAlley.Owner);
            Assert.False(Server.Resolve<ISessionAlleyPlayerDataInterface>()
                .TryGetManagementData(scenario.PlayerAlleyId, out _));
        });

        foreach (var client in Clients)
        {
            client.Call(() => Assert.Equal(
                scenario.GangLeaderId,
                GetId(client, client.GetRegisteredObject<Alley>(scenario.PlayerAlleyId).Owner)));
        }
    }

    [Fact]
    public void ForcedAiAttackThenDefenseWin_ClearsAttackerAndStoresSurvivors()
    {
        var scenario = CreateAlleyDomainScenario();
        var ownerClient = Clients.First();
        string troopId = CreateRegisteredObject<CharacterObject>();
        string ownerCharacterId = GetCharacterId(scenario.OwnerHeroId);

        AcquireAlley(
            ownerClient,
            scenario,
            scenario.OwnerHeroId,
            new TroopRosterElementData(ownerCharacterId, 1, 0, 0),
            new TroopRosterElementData(troopId, 5, 0, 0));

        ForceAttack(scenario);

        AlleyManagementData attacked = GetManagementData(scenario.PlayerAlleyId);
        Assert.Equal(scenario.AttackerAlleyId, attacked.UnderAttackByAlleyId);
        Assert.Same(
            ownerClient.GetRegisteredObject<Alley>(scenario.AttackerAlleyId),
            GetClientAlleyData(ownerClient, scenario.PlayerAlleyId).UnderAttackBy);

        ResolveDefense(
            ownerClient,
            scenario,
            won: true,
            new TroopRosterElementData(ownerCharacterId, 1, 0, 0),
            new TroopRosterElementData(troopId, 2, 0, 10));

        AlleyManagementData resolved = GetManagementData(scenario.PlayerAlleyId);
        Assert.Null(resolved.UnderAttackByAlleyId);
        Assert.Contains(resolved.Garrison,
            element => element.CharacterId == troopId && element.Number == 2 && element.Xp == 10);

        foreach (var instance in Clients.Prepend(Server))
        {
            instance.Call(() => Assert.Null(
                instance.GetRegisteredObject<Alley>(scenario.AttackerAlleyId).Owner));
        }
    }

    [Fact]
    public void ForcedAiAttackThenDefenseLoss_TransfersOwnershipAndRemovesGarrison()
    {
        var scenario = CreateAlleyDomainScenario();
        var ownerClient = Clients.First();
        string ownerCharacterId = GetCharacterId(scenario.OwnerHeroId);

        AcquireAlley(
            ownerClient,
            scenario,
            scenario.OwnerHeroId,
            new TroopRosterElementData(ownerCharacterId, 1, 0, 0));
        ForceAttack(scenario);

        ResolveDefense(ownerClient, scenario, won: false);

        Server.Call(() =>
        {
            Assert.Equal(
                scenario.GangLeaderId,
                GetId(Server, Server.GetRegisteredObject<Alley>(scenario.PlayerAlleyId).Owner));
            Assert.False(Server.Resolve<ISessionAlleyPlayerDataInterface>()
                .TryGetManagementData(scenario.PlayerAlleyId, out _));
        });
        ownerClient.Call(() => Assert.DoesNotContain(
            Campaign.Current.GetCampaignBehavior<AlleyCampaignBehavior>()._playerOwnedCommonAreaData,
            data => data.Alley == ownerClient.GetRegisteredObject<Alley>(scenario.PlayerAlleyId)));
    }

    [Fact]
    public void GangOwnerDeath_CancelsItsAttackAndFreesItsAlley()
    {
        var scenario = CreateAlleyDomainScenario();
        var ownerClient = Clients.First();
        string ownerCharacterId = GetCharacterId(scenario.OwnerHeroId);

        AcquireAlley(
            ownerClient,
            scenario,
            scenario.OwnerHeroId,
            new TroopRosterElementData(ownerCharacterId, 1, 0, 0));
        ForceAttack(scenario);

        KillHero(scenario.GangLeaderId);

        Server.Call(() =>
        {
            Assert.Null(Server.GetRegisteredObject<Alley>(scenario.AttackerAlleyId).Owner);
            Assert.Null(GetManagementData(scenario.PlayerAlleyId).UnderAttackByAlleyId);
        });

        ownerClient.Call(() => Assert.Null(
            GetClientAlleyData(ownerClient, scenario.PlayerAlleyId).UnderAttackBy));
    }

    [Fact]
    public void DeadOverseerAfterGracePeriod_LosesAlleyAndManagementState()
    {
        var scenario = CreateAlleyDomainScenario();
        var ownerClient = Clients.First();
        string overseerCharacterId = GetCharacterId(scenario.OverseerHeroId);

        AcquireAlley(
            ownerClient,
            scenario,
            scenario.OverseerHeroId,
            new TroopRosterElementData(overseerCharacterId, 1, 0, 0));

        KillHero(scenario.OverseerHeroId);

        Server.Call(() =>
        {
            Hero overseer = Server.GetRegisteredObject<Hero>(scenario.OverseerHeroId);
            CampaignTime gracePeriod = Campaign.Current.Models.AlleyModel.DestroyAlleyAfterDaysWhenLeaderIsDeath;
            overseer.SetDeathDay(CampaignTime.Now - gracePeriod - CampaignTime.Days(1f));
            Assert.True(overseer.DeathDay + gracePeriod < CampaignTime.Now);
            Server.Resolve<IMessageBroker>().Publish(this, new AlleyDailyTickTriggered());

            Assert.Null(Server.GetRegisteredObject<Alley>(scenario.PlayerAlleyId).Owner);
            Assert.False(Server.Resolve<ISessionAlleyPlayerDataInterface>()
                .TryGetManagementData(scenario.PlayerAlleyId, out _));
        });
    }

    [Fact]
    public void LocationHostMigration_PreservesServerAuthoritativeAlleyState()
    {
        var scenario = CreateAlleyDomainScenario();
        var clients = Clients.ToArray();
        string ownerCharacterId = GetCharacterId(scenario.OwnerHeroId);

        AcquireAlley(
            clients[0],
            scenario,
            scenario.OwnerHeroId,
            new TroopRosterElementData(ownerCharacterId, 1, 0, 0));
        ForceAttack(scenario);

        MakeLocationReady(clients[0], scenario.InstanceId);
        MakeLocationReady(clients[1], scenario.InstanceId);
        AssertLocationAuthority(Server, scenario.InstanceId, "AlleyOwner", "AlleyOverseer");

        MigrateLocationHost("AlleyOwner", scenario.InstanceId);

        AssertLocationAuthority(Server, scenario.InstanceId, "AlleyOverseer");
        Assert.Equal(
            scenario.AttackerAlleyId,
            GetManagementData(scenario.PlayerAlleyId).UnderAttackByAlleyId);
        Assert.Same(
            clients[0].GetRegisteredObject<Alley>(scenario.AttackerAlleyId),
            GetClientAlleyData(clients[0], scenario.PlayerAlleyId).UnderAttackBy);
    }

    private void KillHero(string heroId)
    {
        Server.Call(() =>
        {
            Hero hero = Server.GetRegisteredObject<Hero>(heroId);
            KillCharacterAction.MakeDead(hero, disbandVictimParty: false);
            Server.Resolve<IMessageBroker>().Publish(this, new AlleyHeroKilledTriggered(hero));
        });
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

    private static string GetId<T>(
        E2E.Tests.Environment.Instance.EnvironmentInstance instance,
        T value)
        where T : class
    {
        Assert.NotNull(value);
        Assert.True(instance.ObjectManager.TryGetId(value, out var id));
        return id;
    }
}
