using Common.Messaging;
using Common.Util;
using E2E.Tests.Services.MapEvents;
using E2E.Tests.Util;
using GameInterface.Services.MapEvents;
using GameInterface.Services.UI.Notifications.Messages;
using SandBox.CampaignBehaviors;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Armies;

public class ArmyDisbanderTests : MapEventTestBase
{
    public ArmyDisbanderTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void LoadedPlayerLedArmyDispersed_ServerOpensNoArmyDispersedMenu()
    {
        var mapEventContext = CreateServerMapEvent();
        var heroId = TestEnvironment.CreateRegisteredObject<Hero>();
        RegisterAsPlayerParty("loaded-player", heroId, mapEventContext.AttackerPartyId);
        var dispersals = new List<bool>();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventContext.MapEventId, out var mapEvent));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(mapEventContext.AttackerPartyId, out var playerLeader));
            var follower = GameObjectCreator.CreateInitializedObject<MobileParty>();
            var army = new Army(GameObjectCreator.CreateInitializedObject<Kingdom>(), playerLeader, Army.ArmyTypes.Raider);
            follower.Army = army;
            follower.AttachedTo = playerLeader;
            Assert.Same(mapEvent, follower.MapEvent);

            // The dedicated server runs vanilla's army wait behavior, which opens a menu for its own player's army
            ObjectHelper.SkipConstructor<PlayerArmyWaitBehavior>().RegisterEvents();
            CampaignEvents.ArmyDispersed.AddNonSerializedListener(this, (_, _, isPlayersArmy) => dispersals.Add(isPlayersArmy));
            Assert.Null(Campaign.Current.CurrentMenuContext);

            Server.Resolve<IMapEventLoadCleaner>().CleanUpLoadedMapEvents();

            Assert.Null(follower.Army);
            // A menu on the server zeroes the campaign dt before TickPartialHourlyAi, so lord and bandit AI stop thinking
            Assert.Null(Campaign.Current.GameMenuManager.NextGameMenuId);
            Assert.False(Assert.Single(dispersals));
        }, MapEventDisabledMethods);
    }

    [Fact]
    public void LoadedPlayerLedArmyDispersed_StillNotifiesThePlayerParty()
    {
        var mapEventContext = CreateServerMapEvent();
        var heroId = TestEnvironment.CreateRegisteredObject<Hero>();
        RegisterAsPlayerParty("loaded-player", heroId, mapEventContext.AttackerPartyId);
        var notifications = new List<ArmyDispersed>();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(mapEventContext.AttackerPartyId, out var playerLeader));
            var follower = GameObjectCreator.CreateInitializedObject<MobileParty>();
            var army = new Army(GameObjectCreator.CreateInitializedObject<Kingdom>(), playerLeader, Army.ArmyTypes.Raider);
            follower.Army = army;
            follower.AttachedTo = playerLeader;

            ObjectHelper.SkipConstructor<DefaultNotificationsCampaignBehavior>().RegisterEvents();
            Server.Resolve<IMessageBroker>().Subscribe<ArmyDispersed>(payload => notifications.Add(payload.What));

            Server.Resolve<IMapEventLoadCleaner>().CleanUpLoadedMapEvents();

            var notification = Assert.Single(notifications);
            Assert.Same(army, notification.Army);
            Assert.Same(playerLeader, Assert.Single(notification.PlayerParties));
        }, MapEventDisabledMethods);
    }
}
