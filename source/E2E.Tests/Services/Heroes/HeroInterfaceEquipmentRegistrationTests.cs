using Common.Serialization;
using E2E.Tests.Environment;
using E2E.Tests.Util;
using GameInterface.Serialization;
using GameInterface.Serialization.External;
using GameInterface.Services.CharacterAttributes;
using GameInterface.Services.Heroes.Data;
using GameInterface.Services.Heroes.Interfaces;
using GameInterface.Services.Players.Data;
using GameInterface.Services.SkillObjects;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Heroes;

/// <summary>
/// A joined player's hero is unpacked under AllowedThread on every machine, so its equipment is only addressable through
/// the ids and handles the join registers.
/// </summary>
public class HeroInterfaceEquipmentRegistrationTests : IDisposable
{
    E2ETestEnvironment TestEnvironment { get; }

    public HeroInterfaceEquipmentRegistrationTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    [Fact]
    public void JoinedHeroEquipment_RegistersUnderServerHandlesAndReceivesSlotWrites()
    {
        var server = TestEnvironment.Server;
        var client = TestEnvironment.Clients.First();

        // The hero package references these by id, as it does in a loaded game
        TestEnvironment.RegisterLoadedObjects<CharacterAttributeRegistry, CharacterAttribute>();
        TestEnvironment.RegisterLoadedObjects<SkillObjectRegistry, SkillObject>();

        byte[] heroData = null!;
        Hero serverHero = null!;
        Player player = null!;
        PlayerRegistrationHandles handles = default;
        string serverBattleEquipmentId = null!;

        server.Call(() =>
        {
            var joiningHero = GameObjectCreator.CreateInitializedObject<Hero>();
            joiningHero._partyBelongedTo = GameObjectCreator.CreateInitializedObject<MobileParty>();

            // Same as PackageMainHero, so the hero graph travels as new objects instead of references
            server.ObjectManager.Remove(joiningHero);
            server.ObjectManager.Remove(joiningHero.PartyBelongedTo);
            server.ObjectManager.Remove(joiningHero.Clan);
            server.ObjectManager.Remove(joiningHero.CharacterObject);
            var package = server.Resolve<IBinaryPackageFactory>().GetBinaryPackage<HeroBinaryPackage>(joiningHero);
            heroData = BinaryPackageSerializer.SerializeCompressed(package);

            var heroInterface = server.Resolve<IHeroInterface>();
            serverHero = heroInterface.ServerUnpackHero(heroData);
            Assert.NotNull(serverHero);
            Assert.True(heroInterface.TryGetRegistrationHandles(serverHero, out handles));
            heroInterface.SetupServerHero(serverHero);

            Assert.True(server.ObjectManager.TryGetId(serverHero, out var heroId));
            Assert.True(server.ObjectManager.TryGetId(serverHero.PartyBelongedTo, out var partyId));
            Assert.True(server.ObjectManager.TryGetId(serverHero.Clan, out var clanId));
            Assert.True(server.ObjectManager.TryGetId(serverHero.CharacterObject, out var characterId));
            player = new Player("joining_client", heroId, partyId, clanId, characterId);

            Assert.True(server.ObjectManager.TryGetId(serverHero._battleEquipment, out serverBattleEquipmentId));
            Assert.True(server.ObjectManager.TryGetHandle(serverHero._battleEquipment, out var serverBattleHandle));
            Assert.Equal(serverBattleHandle, handles.BattleEquipment);
        });

        Hero clientHero = null!;
        client.Call(() =>
        {
            clientHero = client.Resolve<IHeroInterface>().ClientUnpackHero(heroData, player, handles);
            Assert.NotNull(clientHero);
        });

        Assert.True(client.ObjectManager.TryGetId(clientHero._battleEquipment, out var clientBattleEquipmentId));
        Assert.Equal(serverBattleEquipmentId, clientBattleEquipmentId);
        Assert.True(client.ObjectManager.TryGetHandle(clientHero._battleEquipment, out var clientBattleHandle));
        Assert.Equal(handles.BattleEquipment, clientBattleHandle);

        string itemId = TestEnvironment.CreateRegisteredObject<ItemObject>();
        server.Call(() =>
        {
            Assert.True(server.ObjectManager.TryGetObject<ItemObject>(itemId, out var item));
            serverHero.BattleEquipment[EquipmentIndex.Body] = new EquipmentElement(item);
        });

        Assert.True(client.ObjectManager.TryGetObject<ItemObject>(itemId, out var clientItem));
        Assert.Same(clientItem, clientHero.BattleEquipment[EquipmentIndex.Body].Item);
    }
}
