using Common.Messaging;
using Common.Util;
using Coop.Core.Client.Services.MobileParties.Messages;
using Coop.Core.Server.Services.MobileParties.Messages;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Registry.Auto;
using GameInterface.Services.Entity;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Data;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.MapEvents.Messages.Leave;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.PlayerCaptivityService.Messages;
using GameInterface.Services.SiegeEvents.Interfaces;
using GameInterface.Services.TroopRosters.Data;
using HarmonyLib;
using Helpers;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MapEvents;

/// <summary>
/// A winning inside defender of a siege assault gets the battle's prisoner and loot screens before the
/// siege-defeated menu, as in vanilla. The server stages the results on the defender's encounter while it is
/// still in the mission, then tears the event down and prompts the defender victory.
/// </summary>
public class SiegeDefenderVictoryLootTests : MapEventTestBase
{
    private static int defenderVictoryPrompts;
    private static int lootPartyScreens;
    private static int lootInventoryScreens;
    private static int capturedLordConversations;

    public SiegeDefenderVictoryLootTests(ITestOutputHelper output) : base(output) { }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DefenderVictory_ShowsStagedLootBeforeSiegeDefeatedMenu(bool destroyedAfterMissionExit)
    {
        var (client, encounter, _) = StageDefenderVictory(insideTown: false, destroyedAfterMissionExit);

        var harmony = PatchLootScreensAndPrompt();
        try
        {
            client.Call(() =>
            {
                var states = Game.Current.GameStateManager;

                // Back on the map: the prisoner screen opens first, the siege-defeated menu waits.
                MapTick();
                Assert.IsType<PartyState>(states.ActiveState);
                Assert.Equal(PlayerEncounterState.LootInventory, encounter.EncounterState);
                Assert.Equal(0, defenderVictoryPrompts);

                // The encounter menu resuming the same encounter while the screen is open changes nothing.
                PlayerEncounter.Update();
                MapTick();
                Assert.IsType<PartyState>(states.ActiveState);
                Assert.Equal(PlayerEncounterState.LootInventory, encounter.EncounterState);
                Assert.Equal(0, defenderVictoryPrompts);

                states._gameStates.RemoveAt(states._gameStates.Count - 1);
                MapTick();
                Assert.IsType<InventoryState>(states.ActiveState);
                Assert.Equal(0, defenderVictoryPrompts);

                // Both loot screens closed: the encounter finishes, then the siege-defeated menu opens, once.
                states._gameStates.RemoveAt(states._gameStates.Count - 1);
                MapTick();
                Assert.Null(PlayerEncounter.Current);
                Assert.Equal(0, defenderVictoryPrompts);
                MapTick();
                Assert.Equal(1, defenderVictoryPrompts);

                MapTick();
                Assert.Equal(1, defenderVictoryPrompts);
                Assert.Equal(1, lootPartyScreens);
                Assert.Equal(1, lootInventoryScreens);
            }, MapTickDisabledMethods);
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
        }
    }

    [Fact]
    public void DefenderVictory_InsideTown_FinishingTheEncounterDoesNotLeaveTheTown()
    {
        var (client, encounter, settlement) = StageDefenderVictory(insideTown: true);

        var harmony = PatchLootScreensAndPrompt();
        try
        {
            client.Call(() =>
            {
                var states = Game.Current.GameStateManager;

                MapTick();
                Assert.IsType<PartyState>(states.ActiveState);
                states._gameStates.RemoveAt(states._gameStates.Count - 1);
                MapTick();
                Assert.IsType<InventoryState>(states.ActiveState);
                states._gameStates.RemoveAt(states._gameStates.Count - 1);
                MapTick();

                // The encounter is finished and the defender stays in its town.
                Assert.Null(PlayerEncounter.Current);
                Assert.Same(settlement, MobileParty.MainParty._currentSettlement);
                MapTick();
                Assert.Equal(1, defenderVictoryPrompts);
            }, MapTickDisabledMethods);
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
        }
    }

    [Fact]
    public void DefenderVictory_CapturedLord_OpensTheConversationOnceBeforeLoot()
    {
        var (client, encounter, _) = StageDefenderVictory(insideTown: false);
        var lordId = TestEnvironment.CreateRegisteredObject<Hero>();
        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<Hero>(lordId, out var lord));
            encounter.RosterToReceiveLootPrisoners.AddToCounts(lord.CharacterObject, 1);
        }, MapEventDisabledMethods);

        var harmony = PatchLootScreensAndPrompt();
        try
        {
            client.Call(() =>
            {
                var states = Game.Current.GameStateManager;
                var conversations = Campaign.Current.ConversationManager;

                // The conversation opens once and nothing else runs while it is in progress.
                for (var i = 0; i < 3; i++)
                    MapTick();
                Assert.Equal(1, capturedLordConversations);
                Assert.Equal(PlayerEncounterState.CaptureHeroes, encounter.EncounterState);
                Assert.IsType<MapState>(states.ActiveState);
                Assert.Equal(0, defenderVictoryPrompts);

                // Ending it runs vanilla's one shot that drops the lord, then the loot screens and the prompt follow.
                var endOneShot = AccessTools.Field(typeof(ConversationManager), "ConversationEndOneShot");
                var onEnd = (Action)endOneShot.GetValue(conversations)!;
                endOneShot.SetValue(conversations, null);
                onEnd();
                conversations.IsConversationInProgress = false;

                MapTick();
                Assert.IsType<PartyState>(states.ActiveState);
                states._gameStates.RemoveAt(states._gameStates.Count - 1);
                MapTick();
                Assert.IsType<InventoryState>(states.ActiveState);
                states._gameStates.RemoveAt(states._gameStates.Count - 1);
                MapTick();
                MapTick();

                Assert.Null(PlayerEncounter.Current);
                Assert.Equal(1, capturedLordConversations);
                Assert.Equal(1, defenderVictoryPrompts);
            }, MapTickDisabledMethods);
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
        }
    }

    [Fact]
    public void SiegeAssaultAttacker_StagedResults_AreNotResumedFromTheMap()
    {
        var (client, encounter, _) = StageDefenderVictory(insideTown: false);

        var harmony = PatchLootScreensAndPrompt();
        try
        {
            client.Call(() =>
            {
                // The attacker's results stay with vanilla's own siege flow.
                encounter.PlayerSide = BattleSideEnum.Attacker;
                MapTick();
                MapTick();
                Assert.Same(encounter, PlayerEncounter.Current);
                Assert.Equal(PlayerEncounterState.CaptureHeroes, encounter.EncounterState);
                Assert.Equal(0, lootPartyScreens);
                Assert.Equal(0, lootInventoryScreens);
            }, MapTickDisabledMethods);
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
        }
    }

    [Fact]
    public void DefenderVictory_WithoutStagedResults_PromptsOnFirstMapTick()
    {
        var ctx = CreateServerMapEvent();
        var settlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
        var client = Clients.First();
        string? defenderPartyBaseId = null;

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(ctx.DefenderPartyId, out var party));
            Campaign.Current.MainParty = party;
            Assert.True(client.ObjectManager.TryGetId(party.Party, out defenderPartyBaseId));
        }, MapEventDisabledMethods);

        // The stale pre-assault siege encounter, with no results staged on it.
        var encounter = SetMockPlayerEncounter(client);
        client.Call(() =>
        {
            encounter.EncounterState = PlayerEncounterState.Begin;
            var states = Game.Current.GameStateManager;
            states._gameStates.Add(states.CreateState<MapState>());
        }, MapEventDisabledMethods);

        client.SimulateMessage(Server.NetPeer, new NetworkPromptSiegeDefenderVictory(
            settlementId, new[] { defenderPartyBaseId! }));

        var harmony = PatchLootScreensAndPrompt();
        try
        {
            client.Call(() =>
            {
                MapTick();
                Assert.Equal(1, defenderVictoryPrompts);
                Assert.Same(encounter, PlayerEncounter.Current);
                Assert.Equal(PlayerEncounterState.Begin, encounter.EncounterState);

                MapTick();
                Assert.Equal(1, defenderVictoryPrompts);
                Assert.Equal(0, lootPartyScreens);
                Assert.Equal(0, lootInventoryScreens);
            }, MapTickDisabledMethods);
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
        }
    }

    [Fact]
    public void DefenderEncounter_WithoutStagedResults_IsClosedWhenTheEventIsDestroyed()
    {
        var ctx = CreateServerMapEvent();
        var client = Clients.First();

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<MapEvent>(ctx.MapEventId, out var mapEvent));
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(ctx.DefenderPartyId, out var party));
            mapEvent.SetBattleType(MapEvent.BattleTypes.Siege);
            Campaign.Current.MainParty = party;
        }, MapEventDisabledMethods);

        // A defender encounter the server never staged results on, destroyed after the mission is gone.
        var encounter = SetMockPlayerEncounter(client, mapEventId: ctx.MapEventId);
        EnableHeadlessEncounterFinish(client);
        client.Call(() =>
        {
            encounter.PlayerSide = BattleSideEnum.Defender;
            encounter.EncounterState = PlayerEncounterState.Begin;
            encounter._alternativeReceivedLootShips = new List<Ship>();
            var states = Game.Current.GameStateManager;
            states._gameStates.Add(states.CreateState<MapState>());
        }, MapEventDisabledMethods);

        DestroyMapEvent(client, ctx.MapEventId, missionActive: false);

        // The destroy fallback still closes it.
        client.Call(() => Assert.Null(PlayerEncounter.Current), MapEventDisabledMethods);
    }

    [Fact]
    public void DefenderVictoryPrompt_StartsTheTownEncounterLocally()
    {
        const string controllerId = "PlayerOne";
        var client = Clients.First();
        var (_, partyId) = CreatePlayerHeroParty(controllerId);
        TestEnvironment.ConnectRegisteredPlayer(client, controllerId);
        var settlementId = TestEnvironment.CreateRegisteredObject<Settlement>();

        // The server holds the winning defender inside its town.
        foreach (var instance in Clients.Prepend(Server))
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
                Assert.True(instance.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));
                settlement.Party = new PartyBase(settlement);
                party._currentSettlement = settlement;
            });
        }

        client.Resolve<IControllerIdProvider>().SetControllerId(controllerId);
        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Campaign.Current.MainParty = party;
            Campaign.Current.PlayerEncounter = null;
            var states = Game.Current.GameStateManager;
            var map = states.CreateState<MapState>();
            states._gameStates.Add(map);
            map._menuContext = Game.Current.ObjectManager.CreateObject<MenuContext>();
            map._menuContext.GameMenu = ObjectHelper.SkipConstructor<GameMenu>();
            map._menuContext.GameMenu.StringId = "town_outside";
        });

        var harmony = new Harmony($"siege-defender-menu.{Guid.NewGuid()}");
        harmony.Patch(AccessTools.Method(typeof(PlayerEncounter), "Init", new[] { typeof(PartyBase), typeof(PartyBase), typeof(Settlement) }),
            prefix: new HarmonyMethod(typeof(SiegeDefenderVictoryLootTests), nameof(OpenTownMenuOnInit)));
        harmony.Patch(AccessTools.Method(typeof(MenuContext), nameof(MenuContext.SwitchToMenu)),
            prefix: new HarmonyMethod(typeof(SiegeDefenderVictoryLootTests), nameof(SwitchMenu)));
        try
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));
                client.Resolve<ISiegeEventInterface>().PromptSiegeDefenderVictory(settlement);
            });

            // No town encounter request goes out, so no late approval can reopen the town menu.
            Assert.Empty(client.NetworkSentMessages.GetMessages<NetworkRequestStartSettlementEncounter>());
            Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkStartSettlementEncounter>());
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));
                Assert.NotNull(PlayerEncounter.Current);
                Assert.Same(settlement, MobileParty.MainParty.CurrentSettlement);
                Assert.Equal("siege_attacker_defeated", Campaign.Current.CurrentMenuContext.GameMenu.StringId);
            });
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
        }
    }

    // Vanilla PlayerEncounter.Init opens the settlement's encounter menu.
    private static bool OpenTownMenuOnInit()
    {
        Campaign.Current.CurrentMenuContext.GameMenu.StringId = "town_outside";
        return false;
    }

    private static bool SwitchMenu(MenuContext __instance, string menuId)
    {
        __instance.GameMenu.StringId = menuId;
        return false;
    }

    /// <summary>
    /// Stages a siege assault defender victory on the client the way the server delivers it: results committed
    /// while the defender is still in its mission, the event destroyed, then the defender victory prompt. The
    /// destroy arrives either while the mission is still up or after it has popped back to the map.
    /// </summary>
    private (EnvironmentInstance Client, PlayerEncounter Encounter, Settlement Settlement) StageDefenderVictory(
        bool insideTown, bool destroyedAfterMissionExit = true)
    {
        var ctx = CreateServerMapEvent();
        var settlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
        var prisonerId = TestEnvironment.CreateRegisteredObject<CharacterObject>();
        var lootItemId = TestEnvironment.CreateRegisteredObject<ItemObject>();
        var client = Clients.First();
        string? defenderMapEventPartyId = null;
        string? defenderPartyBaseId = null;
        Settlement? settlement = null;

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<ItemObject>(lootItemId, out var item));
            item.StringId = "siege_defender_loot";
            MBObjectManager.Instance.RegisterObject(item);

            Assert.True(client.ObjectManager.TryGetObject<MapEvent>(ctx.MapEventId, out var mapEvent));
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(ctx.DefenderPartyId, out var party));
            Assert.True(client.ObjectManager.TryGetObject(settlementId, out settlement));
            mapEvent.SetBattleType(MapEvent.BattleTypes.Siege);
            Campaign.Current.MainParty = party;
            if (insideTown)
            {
                party._currentSettlement = settlement;
                party.IsActive = true;
            }
            Assert.True(client.ObjectManager.TryGetId(mapEvent.DefenderSide.Parties.Single(), out defenderMapEventPartyId));
            Assert.True(client.ObjectManager.TryGetId(party.Party, out defenderPartyBaseId));
        }, MapEventDisabledMethods);

        var encounter = SetMockPlayerEncounter(client, mapEventId: ctx.MapEventId);
        EnableHeadlessEncounterFinish(client);
        client.Call(() =>
        {
            encounter.PlayerSide = BattleSideEnum.Defender;
            encounter._alternativeReceivedLootShips = new List<Ship>();
            var states = Game.Current.GameStateManager;
            states._gameStates.Add(states.CreateState<MapState>());
        }, MapEventDisabledMethods);

        client.SimulateMessage(Server.NetPeer, new NetworkCommitMapEventResults(
            ctx.MapEventId,
            BattleSideEnum.Defender,
            BattleSideEnum.Defender,
            defenderMapEventPartyId!,
            new NetworkPlayerLootData(
                new Dictionary<string, ItemRosterElement[]>
                {
                    { defenderMapEventPartyId!, new[] { new ItemRosterElement(GetItem(client, lootItemId), 3) } },
                },
                new Dictionary<string, TroopRosterData>(),
                new Dictionary<string, TroopRosterData>
                {
                    {
                        defenderMapEventPartyId!,
                        new TroopRosterData(new[]
                        {
                            new TroopRosterElementData(client.GetHandle<CharacterObject>(prisonerId), 1, 0, 0),
                        })
                    },
                })));

        // Either way the destroy fallback must leave the staged encounter for the map tick to resume.
        DestroyMapEvent(client, ctx.MapEventId, missionActive: !destroyedAfterMissionExit);
        client.SimulateMessage(Server.NetPeer, new NetworkPromptSiegeDefenderVictory(
            settlementId, new[] { defenderPartyBaseId! }));

        client.Call(() =>
        {
            Assert.Same(encounter, PlayerEncounter.Current);
            Assert.Equal(PlayerEncounterState.CaptureHeroes, encounter.EncounterState);
            Assert.True(encounter._mapEvent.IsSiegeAssault);
            Assert.Equal(1, encounter.RosterToReceiveLootPrisoners.TotalManCount);
            Assert.Equal(3, encounter.RosterToReceiveLootItems.Sum(item => item.Amount));
        }, MapEventDisabledMethods);

        return (client, encounter, settlement!);
    }

    private void DestroyMapEvent(EnvironmentInstance client, string mapEventId, bool missionActive)
    {
        client.Call(() =>
        {
            var previousMission = MissionState.Current;
            if (missionActive) MissionState.Current = ObjectHelper.SkipConstructor<MissionState>();
            try
            {
                client.SimulateMessage(Server.NetPeer, new NetworkDestroyInstance<MapEvent>(
                    client.GetHandle<MapEvent>(mapEventId)));
            }
            finally
            {
                MissionState.Current = previousMission;
            }
        }, MapEventDisabledMethods);
    }

    private IReadOnlyList<MethodBase> MapTickDisabledMethods => MapEventDisabledMethods.Concat(new[]
    {
        AccessTools.Method(typeof(Campaign), nameof(Campaign.RealTick)),
        AccessTools.Method(typeof(Campaign), nameof(Campaign.Tick)),
    }).ToList();

    // One campaign map frame: the campaign tick handlers run inside it, the staged result patch after it.
    private static void MapTick() =>
        Game.Current.GameStateManager._gameStates.OfType<MapState>().Last().OnMapModeTick(0.1f);

    private static Harmony PatchLootScreensAndPrompt()
    {
        defenderVictoryPrompts = 0;
        lootPartyScreens = 0;
        lootInventoryScreens = 0;
        capturedLordConversations = 0;
        var harmony = new Harmony($"siege-defender-loot.{Guid.NewGuid()}");
        // CampaignMapConversation.OpenConversation is small enough to be inlined, so stub one level down.
        harmony.Patch(AccessTools.Method(typeof(ConversationManager), nameof(ConversationManager.OpenMapConversation)),
            prefix: new HarmonyMethod(typeof(SiegeDefenderVictoryLootTests), nameof(OpenCapturedLordConversation)));
        harmony.Patch(AccessTools.Method(typeof(PartyScreenHelper), nameof(PartyScreenHelper.OpenScreenAsLoot)),
            prefix: new HarmonyMethod(typeof(SiegeDefenderVictoryLootTests), nameof(OpenLootPartyScreen)));
        harmony.Patch(AccessTools.Method(typeof(InventoryScreenHelper), nameof(InventoryScreenHelper.OpenScreenAsLoot)),
            prefix: new HarmonyMethod(typeof(SiegeDefenderVictoryLootTests), nameof(OpenLootInventoryScreen)));
        harmony.Patch(AccessTools.Method(typeof(SiegeEventInterface), nameof(SiegeEventInterface.PromptSiegeDefenderVictory)),
            prefix: new HarmonyMethod(typeof(SiegeDefenderVictoryLootTests), nameof(CountDefenderVictoryPrompt)));
        return harmony;
    }

    private static ItemObject GetItem(EnvironmentInstance instance, string itemId)
    {
        ItemObject? item = null;
        instance.Call(() => Assert.True(instance.ObjectManager.TryGetObject(itemId, out item)));
        return item!;
    }

    private static bool OpenLootPartyScreen()
    {
        lootPartyScreens++;
        var states = Game.Current.GameStateManager;
        states._gameStates.Add(states.CreateState<PartyState>());
        return false;
    }

    private static bool OpenLootInventoryScreen(Dictionary<PartyBase, ItemRoster> itemRostersToLoot)
    {
        Assert.Equal(3, itemRostersToLoot[PartyBase.MainParty].Sum(item => item.Amount));
        lootInventoryScreens++;
        var states = Game.Current.GameStateManager;
        states._gameStates.Add(states.CreateState<InventoryState>());
        return false;
    }

    private static bool OpenCapturedLordConversation(ConversationManager __instance)
    {
        capturedLordConversations++;
        __instance.IsConversationInProgress = true;
        return false;
    }

    private static bool CountDefenderVictoryPrompt()
    {
        defenderVictoryPrompts++;
        return false;
    }
}