using Common.Messaging;
using Common.Network;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Services.Locations;
using E2E.Tests.Util;
using GameInterface.CoopSessionData;
using GameInterface.Registry.Messages;
using GameInterface.Services.Alleys;
using GameInterface.Services.Alleys.Interfaces;
using GameInterface.Services.Alleys.Messages;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.Players;
using GameInterface.Services.TroopRosters.Data;
using SandBox.CampaignBehaviors;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Alleys;

/// <summary>Builds registered alley management state on the settlement E2E fixture.</summary>
public class AlleyTestEnvironment : SettlementTestEnvironment
{
    public AlleyTestEnvironment(ITestOutputHelper output, int numClients = 2) : base(output, numClients)
    {
        EnsureAlleyBehavior(Server);
        foreach (var client in Clients)
            EnsureAlleyBehavior(client);
    }

    public AlleyDomainScenario CreateAlleyDomainScenario()
    {
        var clients = Clients.ToArray();
        var (instanceId, partyIds) = CreateSettlement("AlleyOwner", "AlleyOverseer");
        string settlementId = instanceId.Substring(0, instanceId.IndexOf('|'));
        string ownerHeroId = GetPlayerHeroId("AlleyOwner");
        string overseerHeroId = GetPlayerHeroId("AlleyOverseer");

        ConfigureHeroIdentity(ownerHeroId);
        ConfigureHeroIdentity(overseerHeroId);
        AddHeroToPartyRoster(ownerHeroId, partyIds[0]);
        AddHeroToPartyRoster(overseerHeroId, partyIds[1]);
        SetMainHero(clients[0], ownerHeroId);
        ConfigureTownSettlement(settlementId, ownerHeroId);

        string playerAlleyId = CreateAlley(settlementId, "player_alley");
        string attackerAlleyId = CreateAlley(settlementId, "gang_alley");
        string gangLeaderId = CreateRegisteredObject<Hero>();
        ConfigureHeroIdentity(gangLeaderId);

        Server.Call(() =>
        {
            Hero gangLeader = Server.GetRegisteredObject<Hero>(gangLeaderId);
            gangLeader.SetNewOccupation(Occupation.GangLeader);
            Assert.True(gangLeader.IsGangLeader);
            Server.GetRegisteredObject<Alley>(attackerAlleyId).SetOwner(gangLeader);
        });

        return new AlleyDomainScenario(
            instanceId,
            settlementId,
            partyIds[0],
            ownerHeroId,
            overseerHeroId,
            playerAlleyId,
            attackerAlleyId,
            gangLeaderId);
    }

    public void AcquireAlley(
        EnvironmentInstance client,
        AlleyDomainScenario scenario,
        string overseerId,
        params TroopRosterElementData[] garrison)
    {
        client.Call(() =>
        {
            Alley alley = client.GetRegisteredObject<Alley>(scenario.PlayerAlleyId);
            Hero owner = client.GetRegisteredObject<Hero>(scenario.OwnerHeroId);
            Hero overseer = client.GetRegisteredObject<Hero>(overseerId);
            TroopRoster roster = CreateRoster(client, garrison);
            client.Resolve<IMessageBroker>().Publish(
                this,
                new AlleyAcquiredRequested(alley, owner, overseer, roster));
        });
    }

    public void ForceAttack(AlleyDomainScenario scenario)
    {
        Server.Call(() => Server.Resolve<IMessageBroker>().Publish(
            this,
            new ForceAlleyAttackRequested(Server.GetRegisteredObject<Alley>(scenario.PlayerAlleyId))));
    }

    public void ResolveDefense(
        EnvironmentInstance client,
        AlleyDomainScenario scenario,
        bool won,
        params TroopRosterElementData[] survivingGarrison)
    {
        client.Call(() => client.Resolve<INetwork>().SendAll(
            new RequestAlleyDefenseResolved(
                scenario.PlayerAlleyId,
                won,
                survivingGarrison)));
    }

    public void SeedLoadedAlleys()
    {
        Server.Call(() => Server.Resolve<IMessageBroker>().Publish(this, new AllGameObjectsRegistered()));
    }

    public void RestoreClientAlleyData(EnvironmentInstance client, string ownerHeroId)
    {
        AlleyPlayerData snapshot = null;
        Server.Call(() =>
        {
            var source = Server.Resolve<ICoopSessionProvider>()
                .CoopSession.AlleyPlayerData.ManagementDataPerAlley;
            var clone = new Dictionary<string, AlleyManagementData>();
            foreach (var pair in source)
            {
                clone[pair.Key] = new AlleyManagementData(
                    pair.Value.OverseerId,
                    pair.Value.Garrison?.ToArray() ?? Array.Empty<TroopRosterElementData>())
                {
                    UnderAttackByAlleyId = pair.Value.UnderAttackByAlleyId,
                    AttackResponseDueDate = pair.Value.AttackResponseDueDate,
                    LastRecruitTimeTicks = pair.Value.LastRecruitTimeTicks
                };
            }
            snapshot = new AlleyPlayerData(clone);
        });

        client.Call(() =>
        {
            Hero owner = client.GetRegisteredObject<Hero>(ownerHeroId);
            client.Resolve<IMessageBroker>().Publish(this, new InitializeClientAlleyData(snapshot));
            client.Resolve<IMessageBroker>().Publish(this, new PlayerHeroChanged(null, owner));
        }, GetNonAlleyPlayerHeroChangedHandlers());
    }

    public AlleyManagementData GetManagementData(string alleyId)
    {
        AlleyManagementData result = null;
        Server.Call(() =>
        {
            Assert.True(Server.Resolve<ISessionAlleyPlayerDataInterface>()
                .TryGetManagementData(alleyId, out result));
        });
        return result;
    }

    public AlleyCampaignBehavior.PlayerAlleyData GetClientAlleyData(
        EnvironmentInstance client,
        string alleyId)
    {
        AlleyCampaignBehavior.PlayerAlleyData result = null;
        client.Call(() =>
        {
            Alley alley = client.GetRegisteredObject<Alley>(alleyId);
            result = Campaign.Current.GetCampaignBehavior<AlleyCampaignBehavior>()
                ._playerOwnedCommonAreaData.Single(data => data.Alley == alley);
        });
        return result;
    }

    public void MakeLocationReady(EnvironmentInstance client, string instanceId)
    {
        MakeLocationMissionReady(client, instanceId);
    }

    public void MigrateLocationHost(string controllerId, string instanceId)
    {
        DepartBattle(controllerId, instanceId, wasRetreat: true);
    }

    public void AssertLocationAuthority(
        EnvironmentInstance instance,
        string instanceId,
        string expectedHost,
        params string[] expectedSuccessors)
    {
        AssertLocationHost(instance, instanceId, expectedHost, expectedSuccessors);
    }

    private void AddHeroToPartyRoster(string heroId, string partyId)
    {
        void Configure(EnvironmentInstance instance)
        {
            instance.Call(() =>
            {
                Hero hero = instance.GetRegisteredObject<Hero>(heroId);
                MobileParty party = instance.GetRegisteredObject<MobileParty>(partyId);
                using (new Common.Util.AllowedThread())
                {
                    if (!party.MemberRoster.Contains(hero.CharacterObject))
                        party.MemberRoster.AddToCounts(hero.CharacterObject, 1);
                }
            });
        }

        Configure(Server);
        foreach (var client in Clients)
            Configure(client);
    }

    private void ConfigureHeroIdentity(string heroId)
    {
        uint id = unchecked((uint)StringComparer.Ordinal.GetHashCode(heroId)) | 1u;

        void Configure(EnvironmentInstance instance)
        {
            instance.Call(() =>
            {
                Hero hero = instance.GetRegisteredObject<Hero>(heroId);
                hero.StringId = heroId;
                hero.Id = new TaleWorlds.ObjectSystem.MBGUID(id);
                if (hero.Clan != null && hero.Clan.Leader == null)
                    hero.Clan.SetLeader(hero);
            });
        }

        Configure(Server);
        foreach (var client in Clients)
            Configure(client);
    }

    private void ConfigureTownSettlement(string settlementId, string ownerHeroId)
    {
        string townId = CreateRegisteredObject<Town>();

        void Configure(EnvironmentInstance instance)
        {
            instance.Call(() =>
            {
                Settlement settlement = instance.GetRegisteredObject<Settlement>(settlementId);
                Hero owner = instance.GetRegisteredObject<Hero>(ownerHeroId);
                Town town = instance.GetRegisteredObject<Town>(townId);
                town.Owner = settlement.Party;
                town._ownerClan = owner.Clan;
                settlement.Town = town;

                var settlements = Campaign.Current.CampaignObjectManager.Settlements;
                if (settlements == null || !settlements.Contains(settlement))
                {
                    var registeredSettlements = settlements == null
                        ? new MBList<Settlement>()
                        : new MBList<Settlement>(settlements);
                    registeredSettlements.Add(settlement);
                    Campaign.Current.CampaignObjectManager.Settlements = registeredSettlements;
                }
            });
        }

        Configure(Server);
        foreach (var client in Clients)
            Configure(client);
    }

    private string CreateAlley(string settlementId, string tag)
    {
        string alleyId = null;
        Server.Call(() =>
        {
            Settlement settlement = Server.GetRegisteredObject<Settlement>(settlementId);
            var alley = new Alley(settlement, tag, new TextObject(tag));
            settlement.Alleys.Add(alley);
            Assert.True(Server.ObjectManager.TryGetId(alley, out alleyId));
        });

        foreach (var client in Clients)
        {
            client.Call(() =>
            {
                Settlement settlement = client.GetRegisteredObject<Settlement>(settlementId);
                Alley alley = client.GetRegisteredObject<Alley>(alleyId);
                settlement.Alleys ??= new List<Alley>();
                if (!settlement.Alleys.Contains(alley)) settlement.Alleys.Add(alley);
            });
        }

        return alleyId;
    }

    private string GetPlayerHeroId(string controllerId)
    {
        string heroId = null;
        Server.Call(() =>
        {
            Assert.True(Server.Resolve<IPlayerManager>().TryGetPlayer(controllerId, out var player));
            heroId = player.HeroId;
        });
        return heroId;
    }

    private static void SetMainHero(EnvironmentInstance client, string heroId)
    {
        client.Call(() =>
        {
            Hero hero = client.GetRegisteredObject<Hero>(heroId);
            Game.Current.PlayerTroop = hero.CharacterObject;
            Assert.Same(hero, Hero.MainHero);
        });
    }

    private static TroopRoster CreateRoster(
        EnvironmentInstance instance,
        IEnumerable<TroopRosterElementData> elements)
    {
        var roster = TroopRoster.CreateDummyTroopRoster();
        foreach (var element in elements)
        {
            CharacterObject character = instance.GetRegisteredObject<CharacterObject>(element.CharacterId);
            roster.AddToCounts(
                character,
                element.Number,
                false,
                element.WoundedNumber,
                element.Xp,
                true,
                -1);
        }
        return roster;
    }

    private static IEnumerable<MethodBase> GetNonAlleyPlayerHeroChangedHandlers()
    {
        Type payloadType = typeof(MessagePayload<PlayerHeroChanged>);
        return typeof(InitializeClientAlleyData).Assembly.GetTypes()
            .Where(type => type.FullName !=
                "GameInterface.Services.Alleys.Handlers.AlleyInitializationHandler")
            .SelectMany(type => type.GetMethods(
                BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            .Where(method =>
            {
                var parameters = method.GetParameters();
                return parameters.Length == 1 && parameters[0].ParameterType == payloadType;
            })
            .Cast<MethodBase>()
            .ToArray();
    }

    private static void EnsureAlleyBehavior(EnvironmentInstance instance)
    {
        instance.Call(() =>
        {
            if (Campaign.Current.GetCampaignBehavior<AlleyCampaignBehavior>() != null) return;
            Campaign.Current.AddCampaignBehaviorManager(new CampaignBehaviorManager(
                new CampaignBehaviorBase[] { new AlleyCampaignBehavior() }));
        });
    }
}

/// <summary>Registry ids for one player alley and its rival gang alley.</summary>
public sealed class AlleyDomainScenario
{
    public string InstanceId { get; }
    public string SettlementId { get; }
    public string OwnerPartyId { get; }
    public string OwnerHeroId { get; }
    public string OverseerHeroId { get; }
    public string PlayerAlleyId { get; }
    public string AttackerAlleyId { get; }
    public string GangLeaderId { get; }

    public AlleyDomainScenario(
        string instanceId,
        string settlementId,
        string ownerPartyId,
        string ownerHeroId,
        string overseerHeroId,
        string playerAlleyId,
        string attackerAlleyId,
        string gangLeaderId)
    {
        InstanceId = instanceId;
        SettlementId = settlementId;
        OwnerPartyId = ownerPartyId;
        OwnerHeroId = ownerHeroId;
        OverseerHeroId = overseerHeroId;
        PlayerAlleyId = playerAlleyId;
        AttackerAlleyId = attackerAlleyId;
        GangLeaderId = gangLeaderId;
    }
}
