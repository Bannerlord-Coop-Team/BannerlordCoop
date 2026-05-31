using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using HarmonyLib;
using SandBox.GauntletUI.Map;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MapEvents;

/// <summary>
/// Base class for MapEvent tests. Sets up the E2E environment with the methods that
/// must be suppressed whenever MapEvent objects are created or destroyed in tests.
/// </summary>
public abstract class MapEventTestBase : IDisposable
{
    internal E2ETestEnvironment TestEnvironment { get; }
    protected EnvironmentInstance Server => TestEnvironment.Server;
    protected IEnumerable<EnvironmentInstance> Clients => TestEnvironment.Clients;

    /// <summary>
    /// Methods that must be disabled when constructing or destroying a <see cref="MapEvent"/>
    /// in the test environment. These normally require a fully loaded game world.
    /// </summary>
    protected IReadOnlyList<MethodBase> MapEventDisabledMethods { get; }

    protected MapEventTestBase(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);

        // MapEventManager.OnMapEventCreated and CampaignEventDispatcher.OnMapEventEnded cannot be
        // patched via PatchScope (they are too small and cause InvalidProgramException under Harmony).
        // Patching MapEventRegistry.OnClientCreated is a single interception point that suppresses both.
        var mapEventRegistryType = AccessTools.TypeByName("GameInterface.Services.MapEvents.MapEventRegistry");

        MapEventDisabledMethods = new List<MethodBase>
        {
            // Covers Campaign.Current.MapEventManager.OnMapEventCreated (called inside OnClientCreated)
            // and other campaign-side registration that requires a fully loaded game world.
            AccessTools.Method(mapEventRegistryType, "OnClientCreated"),
        };
    }

    public void Dispose() => TestEnvironment.Dispose();

    /// <summary>
    /// Creates and registers a <see cref="MapEvent"/> on the server, returning its string ID.
    /// The MapEvent is propagated to all clients via the AutoRegistry.
    /// </summary>
    protected string CreateServerMapEvent()
    {
        string? id = null;
        Server.Call(() =>
        {
            var mapEvent = GameObjectCreator.CreateInitializedObject<MapEvent>();

            // Mirror what MapEventRegistry.OnClientCreated does so the server instance is
            // fully initialized and safe to finalize later.
            mapEvent._sides = new MapEventSide[2];
            mapEvent.WonRounds = new MBList<BattleSideEnum>();

            Assert.True(Server.ObjectManager.TryGetId(mapEvent, out id));
        }, MapEventDisabledMethods);

        Assert.NotNull(id);
        return id!;
    }

    /// <summary>
    /// Creates and registers a <see cref="MapEvent"/> on the server with two <see cref="MobileParty"/>
    /// participants, returning the IDs of the map event, attacker party, and defender party.
    /// </summary>
    protected (string mapEventId, string attackerPartyId, string defenderPartyId) CreateServerMapEventWithParties()
    {
        string? mapEventId = null;
        string? attackerPartyId = null;
        string? defenderPartyId = null;

        Server.Call(() =>
        {
            var mapEvent = GameObjectCreator.CreateInitializedObject<MapEvent>();
            var attackerParty = GameObjectCreator.CreateInitializedObject<MobileParty>();
            var defenderParty = GameObjectCreator.CreateInitializedObject<MobileParty>();

            // The visual must exist before Initialize is called; skip its constructor so
            // we do not need a render context.
            mapEvent.MapEventVisual = ObjectHelper.SkipConstructor<GauntletMapEventVisual>();

            mapEvent.Initialize(attackerParty.Party, defenderParty.Party);

            Assert.True(Server.ObjectManager.TryGetId(mapEvent, out mapEventId));
            Assert.True(Server.ObjectManager.TryGetId(attackerParty, out attackerPartyId));
            Assert.True(Server.ObjectManager.TryGetId(defenderParty, out defenderPartyId));
        }, MapEventDisabledMethods);

        Assert.NotNull(mapEventId);
        Assert.NotNull(attackerPartyId);
        Assert.NotNull(defenderPartyId);

        return (mapEventId!, attackerPartyId!, defenderPartyId!);
    }

    /// <summary>
    /// Finalizes a <see cref="MapEvent"/> on the server and verifies it is removed from all
    /// client object managers.
    /// </summary>
    protected void DestroyServerMapEvent(string mapEventId)
    {
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventId, out var mapEvent));

            using (new AllowedThread())
            {
                mapEvent.FinalizeEvent();
            }
        }, MapEventDisabledMethods);
    }
}
