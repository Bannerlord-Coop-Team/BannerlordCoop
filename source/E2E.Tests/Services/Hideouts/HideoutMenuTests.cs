using E2E.Tests.Services.MapEvents;
using E2E.Tests.Util;
using GameInterface.Services.Barters;
using GameInterface.Services.Hideouts;
using GameInterface.Services.Hideouts.Patches.Disable;
using GameInterface.Services.Villages.Interfaces;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Hideouts;

public class HideoutMenuTests : MapEventTestBase
{
    public HideoutMenuTests(ITestOutputHelper output) : base(output) { }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WaitingMenu_UpdatesWhenTheFirstPlayerStartsOrLeaves(bool leaves)
    {
        var first = CreatePlayerHeroParty("preparing");
        var joining = CreatePlayerHeroParty("waiting");
        string settlementId = null;
        Server.Call(() =>
        {
            var settlement = GameObjectCreator.CreateInitializedObject<Settlement>();
            settlement.SetSettlementComponent(GameObjectCreator.CreateInitializedObject<Hideout>());
            Assert.True(Server.ObjectManager.TryGetId(settlement, out settlementId));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(first.partyId, out var preparingParty));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(joining.partyId, out var waitingParty));
            var banditClan = GameObjectCreator.CreateInitializedObject<Clan>();
            banditClan.Culture = GameObjectCreator.CreateInitializedObject<CultureObject>();
            var bandits = BanditPartyComponent.CreateBanditParty("MenuHideoutBandits", banditClan,
                settlement.Hideout, false, null, new CampaignVec2(Vec2.Zero, true));
            bandits.CurrentSettlement = settlement;
            VillageHostileFactionStanceHelper.ApplyWarStance(preparingParty.MapFaction, bandits.MapFaction);
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(first.heroId, out var hero));
            hero.SetName(new TextObject("Lady Mira"), new TextObject("Mira"));
            preparingParty.IsActive = waitingParty.IsActive = true;
            preparingParty.CurrentSettlement = settlement;
            waitingParty.CurrentSettlement = settlement;
            var state = Server.Resolve<IHideoutPreparation>();
            Assert.Equal(HideoutEntryState.Start, state.GetState(settlement, preparingParty));
            Assert.Equal(HideoutEntryState.Waiting, state.GetState(settlement, waitingParty));
            Campaign.Current.MainParty = null;
        });

        var client = Clients.Last();
        MenuContext context = null;
        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(joining.partyId, out var party));
            Campaign.Current.MainParty = party;
            var starter = new CampaignGameStarter(Campaign.Current.GameMenuManager, Campaign.Current.ConversationManager);
            var behavior = new HideoutCampaignBehavior();
            behavior.OnNewGameCreated(starter);
            behavior.OnSessionLaunched(starter);
            // Native campaign description initialization is separate from menu switching and option conditions.
            Campaign.Current.GameMenuManager.GetGameMenu("hideout_place").OnInit = _ => { };
            context = Game.Current.ObjectManager.CreateObject<MenuContext>();
            var mapState = Game.Current.GameStateManager.CreateState<MapState>();
            mapState._menuContext = context;
            Game.Current.GameStateManager._gameStates.Add(mapState);
            context.SwitchToMenu("hideout_place");

            context.OnTick(0.1f);

            Assert.Equal(HideoutCampaignBehaviorPatch.WaitingMenu, context.GameMenu.StringId);
            Assert.Equal("Waiting for Lady Mira to set up their attack.", context.GameMenu.GetText().ToString());
            Assert.Contains(context.GameMenu.MenuOptions, option => option.IdString == "leave");
            Assert.DoesNotContain(context.GameMenu.MenuOptions, option => option.IdString == "coop_join_hideout");
            var args = new MenuCallbackArgs(context, TextObject.GetEmpty());
            Assert.False(behavior.game_menu_hideout_sneak_in_on_condition(args));
            Assert.False(behavior.game_menu_assault_hideout_parties_on_condition(args));
            Assert.False(behavior.game_menu_send_troops_hideout_on_condition(args));
            Assert.False(behavior.game_menu_wait_until_nightfall_on_condition(args));
        });

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(first.partyId, out var party));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));
            if (leaves)
                party.CurrentSettlement = null;
            else
            {
                Assert.True(Server.ObjectManager.TryGetObject<Hero>(first.heroId, out var hero));
                using var playerContext = new BarterPlayerContext(hero, party);
                var battle = GameObjectCreator.CreateInitializedObject<MapEvent>();
                battle.MapEventVisual = MockMapEventVisual();
                battle.Initialize(party.Party, settlement.Party, new HideoutEventComponent(battle, false), MapEvent.BattleTypes.Hideout);
                battle.MapEventVisual = null;
                Campaign.Current.MapEventManager.OnMapEventCreated(battle);
            }
        }, MapEventDisabledMethods);

        client.Call(() =>
        {
            var previousMission = MissionState.Current;
            MissionState.Current = new MissionState();
            try
            {
                context.OnTick(0.1f);
                Assert.Equal(HideoutCampaignBehaviorPatch.WaitingMenu, context.GameMenu.StringId);
            }
            finally { MissionState.Current = previousMission; }
            context.OnTick(0.1f);

            Assert.Equal(leaves ? "hideout_place" : HideoutCampaignBehaviorPatch.JoinMenu, context.GameMenu.StringId);
            if (leaves)
            {
                Assert.Contains(context.GameMenu.MenuOptions, option => option.IdString == "assault");
                Assert.Contains(context.GameMenu.MenuOptions, option => option.IdString == "attack");
            }
            else
            {
                Assert.Equal("Lady Mira has started the hideout attack.", context.GameMenu.GetText().ToString());
                var join = Assert.Single(context.GameMenu.MenuOptions, option => option.IdString == "coop_join_hideout");
                Assert.True(join.GetConditionsHold(Game.Current, context));
                Assert.DoesNotContain(context.GameMenu.MenuOptions, option => option.IdString == "assault");
                Assert.DoesNotContain(context.GameMenu.MenuOptions, option => option.IdString == "attack");
            }
        });
    }
}
