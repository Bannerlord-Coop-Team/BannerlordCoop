using E2E.Tests.Environment;
using E2E.Tests.Util;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using Xunit.Abstractions;

namespace E2E.Tests.Services.PlayerCaptivityService;

/// <summary>
/// Checks that a captive server main hero doesn't open the captivity menu, which would stop every party's AI.
/// </summary>
public class ServerMainHeroCaptivityTests : IDisposable
{
    private readonly E2ETestEnvironment environment;

    public ServerMainHeroCaptivityTests(ITestOutputHelper output)
    {
        environment = new E2ETestEnvironment(output);
    }

    [Fact]
    public void ServerTick_CaptiveMainHero_KeepsPartyAiRunning()
    {
        var server = environment.Server;
        var partyId = environment.CreateRegisteredObject<MobileParty>();
        var captorId = environment.CreateRegisteredObject<MobileParty>();
        using var partiesThink = new MethodCallRecorder(Priority.First, AccessTools.Method(typeof(Campaign), "PartiesThink"));

        // The tick systems around PlayerCaptivity.Update need a full campaign the test doesn't have.
        var disabledMethods = new[]
        {
            AccessTools.Method(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.Tick)),
            AccessTools.Method(typeof(CampaignPeriodicEventManager), nameof(CampaignPeriodicEventManager.OnTick)),
            AccessTools.Method(typeof(CampaignPeriodicEventManager), nameof(CampaignPeriodicEventManager.TickPeriodicEvents)),
            AccessTools.Method(typeof(CampaignPeriodicEventManager), nameof(CampaignPeriodicEventManager.TickPartialHourlyAi)),
            AccessTools.Method(typeof(CampaignTickCacheDataStore), nameof(CampaignTickCacheDataStore.Tick)),
            AccessTools.Method(typeof(MapEventManager), nameof(MapEventManager.Tick)),
            AccessTools.Method(typeof(EncounterManager), nameof(EncounterManager.Tick)),
        };

        server.Call(() =>
        {
            Assert.True(server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.True(server.ObjectManager.TryGetObject<MobileParty>(captorId, out var captor));
            Campaign.Current.MainParty = party;
            Campaign.Current.MapStateData ??= new MapStateData();
            captor.IsActive = true;

            var states = Game.Current.GameStateManager;
            var mapState = states.CreateState<MapState>();
            states._gameStates.Add(mapState);

            // PlayerTownVisitCampaignBehavior registers prisoner_wait in game; an empty wait menu is enough here.
            var starter = new CampaignGameStarter(Campaign.Current.GameMenuManager, Campaign.Current.ConversationManager);
            var behavior = new PlayerCaptivityCampaignBehavior();
            behavior.OnSessionLaunched(starter);
            starter.AddWaitGameMenu("prisoner_wait", "", null, null, null, null, GameMenu.MenuAndOptionType.WaitMenuHideProgressAndHoursOption);
            Campaign.Current.PlayerCaptivity._captivityCampaignBehavior = behavior;

            // A captor not at war with the main hero makes CheckCaptivityChange pick a plain menu.
            TakePrisonerAction.Apply(captor.Party, Hero.MainHero);
            Assert.True(PlayerCaptivity.IsCaptive);

            // MobilePartyHourlyTick is inlined into Tick, so it can't be disabled; give its ticker nothing to tick.
            Campaign.Current._campaignPeriodicEventManager._mobilePartyHourlyTicker.Initialize(new MBList<MobileParty>(), _ => { }, false);
            Campaign.Current._dt = 0.01f;
            Campaign.Current.CurrentTickCount = 10;
            Campaign.Current.Tick();
            Campaign.Current.LateAITickAux();

            Assert.Null(mapState.GameMenuId);
            Assert.False(mapState.AtMenu);
            Assert.True(Campaign.Current._dt > 0f);
            Assert.Equal(1, partiesThink.Count);
        }, disabledMethods);
    }

    public void Dispose() => environment.Dispose();
}
