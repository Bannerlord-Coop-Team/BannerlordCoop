using Common.Messaging;
using E2E.Tests.Environment.Instance;
using GameInterface.Registry.Auto;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Data;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.MapEvents.Messages.Leave;
using GameInterface.Services.PlayerCaptivityService.Messages;
using GameInterface.Services.SiegeEvents.Interfaces;
using GameInterface.Services.TroopRosters.Data;
using HarmonyLib;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
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

    public SiegeDefenderVictoryLootTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void DefenderVictory_ShowsStagedLootBeforeSiegeDefeatedMenu()
    {
        var ctx = CreateServerMapEvent();
        var settlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
        var prisonerId = TestEnvironment.CreateRegisteredObject<CharacterObject>();
        var lootItemId = TestEnvironment.CreateRegisteredObject<ItemObject>();
        var client = Clients.First();
        string? defenderMapEventPartyId = null;
        string? defenderPartyBaseId = null;

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<ItemObject>(lootItemId, out var item));
            item.StringId = "siege_defender_loot";
            MBObjectManager.Instance.RegisterObject(item);

            Assert.True(client.ObjectManager.TryGetObject<MapEvent>(ctx.MapEventId, out var mapEvent));
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(ctx.DefenderPartyId, out var party));
            Campaign.Current.MainParty = party;
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

        // The defender is still in its siege mission when the server destroys the event, so the destroy
        // fallback leaves the staged encounter to the mission flow.
        client.Call(() => client.SimulateMessage(Server.NetPeer, new NetworkDestroyInstance<MapEvent>(
                client.GetHandle<MapEvent>(ctx.MapEventId))),
            MapEventDisabledMethods
                .Append(AccessTools.Method(typeof(MapEventRegistry), "CloseDestroyedMapEventEncounterIfNeeded"))
                .ToList());
        client.SimulateMessage(Server.NetPeer, new NetworkPromptSiegeDefenderVictory(
            settlementId, new[] { defenderPartyBaseId! }));

        client.Call(() =>
        {
            Assert.Same(encounter, PlayerEncounter.Current);
            Assert.Equal(PlayerEncounterState.CaptureHeroes, encounter.EncounterState);
            Assert.Equal(1, encounter.RosterToReceiveLootPrisoners.TotalManCount);
            Assert.Equal(3, encounter.RosterToReceiveLootItems.Sum(item => item.Amount));
        }, MapEventDisabledMethods);

        defenderVictoryPrompts = 0;
        var harmony = new Harmony($"siege-defender-loot.{Guid.NewGuid()}");
        harmony.Patch(AccessTools.Method(typeof(PartyScreenHelper), nameof(PartyScreenHelper.OpenScreenAsLoot)),
            prefix: new HarmonyMethod(typeof(SiegeDefenderVictoryLootTests), nameof(OpenLootPartyScreen)));
        harmony.Patch(AccessTools.Method(typeof(InventoryScreenHelper), nameof(InventoryScreenHelper.OpenScreenAsLoot)),
            prefix: new HarmonyMethod(typeof(SiegeDefenderVictoryLootTests), nameof(OpenLootInventoryScreen)));
        harmony.Patch(AccessTools.Method(typeof(SiegeEventInterface), nameof(SiegeEventInterface.PromptSiegeDefenderVictory)),
            prefix: new HarmonyMethod(typeof(SiegeDefenderVictoryLootTests), nameof(CountDefenderVictoryPrompt)));
        try
        {
            client.Call(() =>
            {
                var states = Game.Current.GameStateManager;
                var broker = client.Resolve<IMessageBroker>();

                // Back on the map: the prisoner screen opens first, the siege-defeated menu waits.
                broker.Publish(this, new CampaignTick());
                Assert.IsType<PartyState>(states.ActiveState);
                Assert.Equal(PlayerEncounterState.LootInventory, encounter.EncounterState);
                Assert.Equal(0, defenderVictoryPrompts);

                states._gameStates.RemoveAt(states._gameStates.Count - 1);
                broker.Publish(this, new CampaignTick());
                Assert.IsType<InventoryState>(states.ActiveState);
                Assert.Equal(0, defenderVictoryPrompts);

                // Both loot screens closed: the defender moves on to the siege-defeated menu, once.
                states._gameStates.RemoveAt(states._gameStates.Count - 1);
                broker.Publish(this, new CampaignTick());
                Assert.Equal(PlayerEncounterState.End, encounter.EncounterState);
                Assert.Equal(1, defenderVictoryPrompts);

                broker.Publish(this, new CampaignTick());
                Assert.Equal(1, defenderVictoryPrompts);
            }, MapEventDisabledMethods);
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
        }
    }

    private static ItemObject GetItem(EnvironmentInstance instance, string itemId)
    {
        ItemObject? item = null;
        instance.Call(() => Assert.True(instance.ObjectManager.TryGetObject(itemId, out item)));
        return item!;
    }

    private static bool OpenLootPartyScreen()
    {
        var states = Game.Current.GameStateManager;
        states._gameStates.Add(states.CreateState<PartyState>());
        return false;
    }

    private static bool OpenLootInventoryScreen(Dictionary<PartyBase, ItemRoster> itemRostersToLoot)
    {
        Assert.Equal(3, itemRostersToLoot[PartyBase.MainParty].Sum(item => item.Amount));
        var states = Game.Current.GameStateManager;
        states._gameStates.Add(states.CreateState<InventoryState>());
        return false;
    }

    private static bool CountDefenderVictoryPrompt()
    {
        defenderVictoryPrompts++;
        return false;
    }
}
