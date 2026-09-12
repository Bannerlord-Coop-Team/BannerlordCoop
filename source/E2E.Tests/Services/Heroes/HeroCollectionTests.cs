using E2E.Tests.Environment;
using E2E.Tests.Util.ObjectBuilders;
using GameInterface.Services.Heroes.Messages.Collections;
using GameInterface.Services.Heroes.Patches;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Heroes;

public class HeroCollectionTests : IDisposable
{
    E2ETestEnvironment TestEnvironment { get; }
    private string ChildId;
    private string HeroId;
    private string CharacterObjectId;
    private string WorkshopId;
    private string CaravanId;

    public HeroCollectionTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    [Fact]
    public void ServerUpdateHeroCollection_SyncAllClients()
    {
        // Arrange
        var server = TestEnvironment.Server;
        CharacterObject Character = null;
        Hero Child = null;
        Workshop workshop = null;
        CaravanPartyComponent caravan = null;

        // Act

        server.Call(() =>
        {
            HeroId = TestEnvironment.CreateRegisteredObject<Hero>();
            ChildId = TestEnvironment.CreateRegisteredObject<Hero>();
            Assert.True(server.ObjectManager.TryGetObject<Hero>(HeroId, out var Hero));
            Assert.True(server.ObjectManager.TryGetObject<Hero>(ChildId, out Child));

            HeroCollectionPatches.ListAddIntercept<Hero, ChildrenListUpdated>(Hero._children, Child, Hero);
            Assert.Equal( Child, Hero._children.Last());

            CharacterObjectId = TestEnvironment.CreateRegisteredObject<CharacterObject>();
            Assert.True(server.ObjectManager.TryGetObject<CharacterObject>(CharacterObjectId, out Character));

            Assert.NotEqual(Character, Hero.VolunteerTypes[0]);
            HeroCollectionPatches.ArrayAssignIntercept(Hero.VolunteerTypes, 0, Character, Hero);
            Assert.Equal(Character, Hero.VolunteerTypes[0]);

            WorkshopId = TestEnvironment.CreateRegisteredObject<Workshop>();
            Assert.True(server.ObjectManager.TryGetObject<Workshop>(WorkshopId, out workshop));

            Assert.Empty(Hero.OwnedWorkshops);
            HeroCollectionPatches.MBListAddIntercept<Workshop, WorkshopListUpdated>(Hero._ownedWorkshops, workshop, Hero);
            Assert.Equal(workshop, Hero.OwnedWorkshops.Last());

            var componentBuilder = new CaravanPartyComponentBuilder();
            caravan = componentBuilder.BuildWithHero(Hero);
            Assert.True(server.ObjectManager.TryGetId(caravan, out CaravanId));

            Assert.Empty(Hero.OwnedCaravans);
            HeroCollectionPatches.ListAddIntercept<CaravanPartyComponent, CaravanListUpdated>(Hero.OwnedCaravans, caravan, Hero);
            Assert.Equal(caravan, Hero.OwnedCaravans.Last());
        });

        TestEnvironment.FlushCoalescer();

        // Assert
        Assert.True(server.ObjectManager.TryGetObject<Hero>(HeroId, out var Hero));

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<Hero>(HeroId, out var clientHero));
            Assert.Equal(Child.StringId, clientHero.Children.Last().StringId);  // Some fields are not synced like BannerItem,  
            Assert.Equal(Character.StringId, clientHero.VolunteerTypes[0].StringId);  // CharacterObject props/fields is not synced yet
            Assert.Equal(workshop.Tag, clientHero.OwnedWorkshops.Last().Tag);
            Assert.NotEmpty(clientHero.OwnedCaravans); // Some fields like (cached)Name are not synced for (Caravan)PartyComponent
        }

        // Remove
        server.Call(() =>
        {
            HeroCollectionPatches.MBListRemoveIntercept<Workshop, WorkshopListRemoved>(Hero._ownedWorkshops, workshop, Hero);
            Assert.Empty(Hero.OwnedWorkshops);

            HeroCollectionPatches.ListRemoveIntercept<CaravanPartyComponent, CaravanListRemoved>(Hero.OwnedCaravans, caravan, Hero);
            Assert.Empty(Hero.OwnedCaravans);
        });

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<Hero>(HeroId, out var clientHero));
            Assert.Empty(clientHero.OwnedWorkshops);
            Assert.Empty(clientHero.OwnedCaravans);
        }
    }

    [Fact]
    public void ClientUpdateHeroCollection_DoesNothing()
    {
        // Arrange
        var server = TestEnvironment.Server;
        CharacterObject Character = null;
        Hero Child = null;

        server.Call(() =>
        {
            HeroId = TestEnvironment.CreateRegisteredObject<Hero>();
            ChildId = TestEnvironment.CreateRegisteredObject<Hero>();
            Assert.True(server.ObjectManager.TryGetObject<Hero>(HeroId, out var Hero));
            Assert.True(server.ObjectManager.TryGetObject<Hero>(ChildId, out Child));

            CharacterObjectId = TestEnvironment.CreateRegisteredObject<CharacterObject>();
            Assert.True(server.ObjectManager.TryGetObject<CharacterObject>(CharacterObjectId, out Character));
        });

        // Act
        var firstClient = TestEnvironment.Clients.First();
        firstClient.Call(() =>
        {
            Assert.True(server.ObjectManager.TryGetObject<Hero>(HeroId, out var clientHero));
            Assert.True(server.ObjectManager.TryGetObject<Hero>(ChildId, out var clientChild));
            HeroCollectionPatches.ListAddIntercept<Hero, ChildrenListUpdated>(clientHero._children, clientChild, clientHero);

            Assert.True(server.ObjectManager.TryGetObject<CharacterObject>(CharacterObjectId, out Character));
            HeroCollectionPatches.ArrayAssignIntercept(clientHero.VolunteerTypes, 0, Character, clientHero);
        });

        // Assert
        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<Hero>(HeroId, out var clientHero));
            if (clientHero.Children.Count > 0)
            {
                Assert.NotEqual(Child.StringId, clientHero.Children.Last().StringId);
            }
            Assert.Null(clientHero.VolunteerTypes[0]);
        }
    }
}
