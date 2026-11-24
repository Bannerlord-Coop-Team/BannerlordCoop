using Common.Messaging;
using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.Equipments.Messages;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Equipments;

/// <summary>
/// Covers a client's main hero losing or laming their horse in a mission, which the server applies for every machine.
/// </summary>
public class MainHeroMountHandlerTests : IDisposable
{
    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;

    public MainHeroMountHandlerTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    [Theory]
    [InlineData(MountLoss.Lost)]
    [InlineData(MountLoss.Lamed)]
    public void MainHeroMountKilled_ServerAndEveryClientHoldTheResult(MountLoss loss)
    {
        var client = TestEnvironment.Clients.First();

        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        var horseId = TestEnvironment.CreateRegisteredObject<ItemObject>();
        var harnessId = TestEnvironment.CreateRegisteredObject<ItemObject>();

        foreach (var instance in TestEnvironment.Clients.Prepend(Server))
        {
            instance.Call(() =>
            {
                // Roster packets resolve items through the native catalog, as a loaded campaign does
                MBObjectManager.Instance.RegisterObject(instance.GetRegisteredObject<ItemObject>(harnessId));

                // A loaded campaign registers every modifier under its vanilla id
                Assert.True(instance.ObjectManager.AddExisting(MainHeroMountKilled.LameHorseModifierId, ObjectHelper.SkipConstructor<ItemModifier>()));
            });
        }

        string? heroId = null;
        Server.Call(() =>
        {
            var party = Server.GetRegisteredObject<MobileParty>(partyId);
            var hero = party.LeaderHero;
            Assert.Same(party, hero.PartyBelongedTo);
            Assert.True(Server.ObjectManager.TryGetId(hero, out heroId));
            Assert.True(Server.Resolve<IPlayerManager>().AddPlayer(new Player("mount-rider", heroId, partyId, "", "")));

            hero.BattleEquipment[EquipmentIndex.Horse] = new EquipmentElement(Server.GetRegisteredObject<ItemObject>(horseId));
            hero.BattleEquipment[EquipmentIndex.HorseHarness] = new EquipmentElement(Server.GetRegisteredObject<ItemObject>(harnessId));
        });
        TestEnvironment.ConnectRegisteredPlayer(client, "mount-rider");

        client.Call(() => client.Resolve<IMessageBroker>().Publish(this,
            new MainHeroMountKilled(client.GetRegisteredObject<Hero>(heroId!), usesCivilianEquipment: false, loss)));
        TestEnvironment.FlushCoalescer();

        foreach (var instance in TestEnvironment.Clients.Prepend(Server))
        {
            instance.Call(() =>
            {
                var party = instance.GetRegisteredObject<MobileParty>(partyId);
                var equipment = party.LeaderHero.BattleEquipment;
                var harness = instance.GetRegisteredObject<ItemObject>(harnessId);

                if (loss == MountLoss.Lost)
                {
                    Assert.Null(equipment[EquipmentIndex.Horse].Item);
                    Assert.Null(equipment[EquipmentIndex.HorseHarness].Item);
                    Assert.Equal(1, party.ItemRoster.GetItemNumber(harness));
                }
                else
                {
                    Assert.Same(instance.GetRegisteredObject<ItemObject>(horseId), equipment[EquipmentIndex.Horse].Item);
                    Assert.Same(instance.GetRegisteredObject<ItemModifier>(MainHeroMountKilled.LameHorseModifierId), equipment[EquipmentIndex.Horse].ItemModifier);
                    Assert.Same(harness, equipment[EquipmentIndex.HorseHarness].Item);
                    Assert.Equal(0, party.ItemRoster.GetItemNumber(harness));
                }
            });
        }
    }
}
