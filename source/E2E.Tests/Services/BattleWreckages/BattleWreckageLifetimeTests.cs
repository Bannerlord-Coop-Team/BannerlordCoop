using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.BattleWreckages;
using GameInterface.Services.BattleWreckages.Data;
using GameInterface.Services.BattleWreckages.Interfaces;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.BattleWreckages;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using Xunit.Abstractions;

namespace E2E.Tests.Services.BattleWreckages;

public class BattleWreckageLifetimeTests : IDisposable
{
    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;
    private IEnumerable<EnvironmentInstance> Clients => TestEnvironment.Clients;

    public BattleWreckageLifetimeTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    [Fact]
    public void ServerCreate_BattleWreckage_SyncAllClients()
    {
        // Act
        var battleWreckageId = CreateServerWreckage();

        // Assert
        foreach (var client in Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<BattleWreckage>(battleWreckageId, out var battleWreckage));
                Assert.Contains(battleWreckage, Campaign.Current.Wreckages);
                Assert.Equal(BattleWreckage.WreckageType.Small, battleWreckage.WreckageTypeCategory);
            });
        }
    }

    [Fact]
    public void ServerCreate_BattleWreckage_SyncsRosters()
    {
        // Arrange
        var battleWreckageId = CreateServerWreckage();

        // Act
        var serverRosterIds = new List<string>();
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<BattleWreckage>(battleWreckageId, out var battleWreckage));

            var character = GameObjectCreator.CreateInitializedObject<CharacterObject>();
            battleWreckage.AttackerDiedInBattle.AddToCounts(character, 3);

            foreach (var roster in GetRosters(battleWreckage))
            {
                Assert.True(Server.ObjectManager.TryGetId(roster, out var rosterId));
                serverRosterIds.Add(rosterId);
            }
        });

        // Ownerless roster changes are coalesced and sent on the server's next tick
        TestEnvironment.FlushCoalescer();

        // Assert
        foreach (var client in Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<BattleWreckage>(battleWreckageId, out var battleWreckage));

                var clientRosterIds = new List<string>();
                foreach (var roster in GetRosters(battleWreckage))
                {
                    Assert.True(client.ObjectManager.TryGetId(roster, out var rosterId));
                    clientRosterIds.Add(rosterId);
                }

                Assert.Equal(serverRosterIds, clientRosterIds);
                Assert.Equal(3, battleWreckage.AttackerDiedInBattle.TotalManCount);
            });
        }
    }

    [Fact]
    public void ServerDestroy_BattleWreckage_SyncAllClients()
    {
        // Arrange
        var battleWreckageId = CreateServerWreckage();

        var clientWreckages = new Dictionary<EnvironmentInstance, BattleWreckage>();
        foreach (var client in Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<BattleWreckage>(battleWreckageId, out var battleWreckage));
                clientWreckages[client] = battleWreckage;
            });
        }

        // Act
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<BattleWreckage>(battleWreckageId, out var battleWreckage));

            battleWreckage.DestroyWreckage();
        });

        // Assert
        foreach (var client in Clients)
        {
            client.Call(() =>
            {
                Assert.False(client.ObjectManager.TryGetObject<BattleWreckage>(battleWreckageId, out _));
                Assert.DoesNotContain(clientWreckages[client], Campaign.Current.Wreckages);
            });
        }
    }

    [Fact]
    public void ServerLeave_InvestigationCompleted_SyncsInvestigatedToAllClients()
    {
        // Arrange
        var battleWreckageId = CreateServerWreckage();

        // Act
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<BattleWreckage>(battleWreckageId, out var battleWreckage));

            // Epic wreckages stay on the map once investigated, so clients still have one to check. Creating one as Epic
            // needs a settlement for its name, and the field is readonly
            AccessTools.Field(typeof(BattleWreckage), nameof(BattleWreckage.WreckageTypeCategory))
                .SetValue(battleWreckage, BattleWreckage.WreckageType.Epic);

            var hero = GameObjectCreator.CreateInitializedObject<Hero>();
            var encounterTracker = Server.Resolve<IBattleWreckageEncounterTracker>();
            encounterTracker.AddEncounter(new BattleWreckageEncounter(hero, battleWreckage));

            Server.Resolve<IBattleWreckageCampaignBehaviorInterface>().LeaveWreckageEncounter(
                hero,
                BattleWreckageCampaignBehavior.WreckageLeaveReason.InvestigationCompleted);

            Assert.False(encounterTracker.TryGetEncounter(hero, out _));
        });

        // Assert
        foreach (var client in Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<BattleWreckage>(battleWreckageId, out var battleWreckage));
                Assert.True(battleWreckage.IsInvestigated);
            });
        }
    }

    private string CreateServerWreckage()
    {
        string? battleWreckageId = null;

        Server.Call(() =>
        {
            var mapEvent = GameObjectCreator.CreateInitializedObject<MapEvent>();
            SetSides(mapEvent,
                GameObjectCreator.CreateInitializedObject<MobileParty>(),
                GameObjectCreator.CreateInitializedObject<MobileParty>());

            BattleWreckage.CreateWreckage(mapEvent, BattleWreckage.WreckageType.Small, CampaignTime.Zero);

            var battleWreckage = Campaign.Current.Wreckages.Last();

            Assert.True(Server.ObjectManager.TryGetId(battleWreckage, out battleWreckageId));
        });

        Assert.NotNull(battleWreckageId);

        return battleWreckageId!;
    }

    // The wreckage constructor reads both sides' leader parties and their names. Custom names skip the
    // lord party name lookup, which needs a fully built leader hero
    private static void SetSides(MapEvent mapEvent, MobileParty attackerParty, MobileParty defenderParty)
    {
        attackerParty.Party.SetCustomName(new TextObject("Attackers"));
        defenderParty.Party.SetCustomName(new TextObject("Defenders"));

        mapEvent._sides[(int)BattleSideEnum.Attacker] = new MapEventSide(mapEvent, BattleSideEnum.Attacker, attackerParty.Party);
        mapEvent._sides[(int)BattleSideEnum.Defender] = new MapEventSide(mapEvent, BattleSideEnum.Defender, defenderParty.Party);
    }

    private static TroopRoster[] GetRosters(BattleWreckage battleWreckage) => new[]
    {
        battleWreckage.AttackerDiedInBattle,
        battleWreckage.DefenderDiedInBattle,
        battleWreckage.AttackerWoundedInBattle,
        battleWreckage.DefenderWoundedInBattle,
    };
}
