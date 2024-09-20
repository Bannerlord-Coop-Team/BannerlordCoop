using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using HarmonyLib;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using Xunit.Abstractions;


namespace E2E.Tests.Services.Equipments;
public class EquipmentTests : IDisposable
{
    E2ETestEnvironment TestEnvironement { get; }


    EnvironmentInstance Server => TestEnvironement.Server;
    IEnumerable<EnvironmentInstance> Clients => TestEnvironement.Clients;


    string HeroId { get; set; }
    int CivHeroEquipmentType { get; set; }

    public EquipmentTests(ITestOutputHelper output)
    {
        TestEnvironement = new E2ETestEnvironment(output);


        Server.Call(() =>
        {

            var hero = GameObjectCreator.CreateInitializedObject<Hero>();
            HeroId = hero.StringId;

        });
    }

    public void Dispose()
    {
        TestEnvironement.Dispose();
    }

    [Fact]
    public void ServerChangeEquipmentType_SyncAllClients()
    {
        Assert.True(Server.ObjectManager.TryGetObject<Hero>(HeroId, out var serverHero));

        // Act
        Server.Call(() =>
        {
            serverHero.BattleEquipment._equipmentType = serverHero.CivilianEquipment._equipmentType;

        });


        // Assert
        Assert.Equal(serverHero.BattleEquipment._equipmentType, serverHero.CivilianEquipment._equipmentType);

        foreach (var client in TestEnvironement.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<Hero>(HeroId, out var HeroParty));
            Assert.Equal((int)HeroParty.BattleEquipment._equipmentType, (int)serverHero.BattleEquipment._equipmentType);
        }
    }

    [Fact]
    public void ClientAttachedTo_NoChange()
    {
        Assert.True(Server.ObjectManager.TryGetObject<Hero>(HeroId, out var serverHero));
        


        Server.Call(() =>
        {
            serverHero.ResetEquipments();
            Assert.NotNull(serverHero.BattleEquipment);

        });

        // Act
        var firstClient = Clients.First();


        firstClient.Call(() =>
        {
            Assert.True(firstClient.ObjectManager.TryGetObject<Hero>(HeroId, out var clientHero));

            // Hero property battle equipment needs to be synced.
      //      clientHero.BattleEquipment._equipmentType = (TaleWorlds.Core.Equipment.EquipmentType)(-1);
        });
 

        // Assert
        foreach (var client in TestEnvironement.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<Hero>(HeroId, out var clientHero));
            Assert.NotNull(clientHero.BattleEquipment);
            Assert.Equal(clientHero.BattleEquipment._equipmentType, serverHero.BattleEquipment._equipmentType);
        }
    }
}