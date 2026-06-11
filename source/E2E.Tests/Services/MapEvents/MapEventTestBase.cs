using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using HarmonyLib;
using Moq;
using SandBox.GauntletUI.Map;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameComponents;
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

        MapEventDisabledMethods = new List<MethodBase>
        {
            // MapEvent.Initialize drives the map-event visual's Initialize, which needs a live render
            // context (GauntletUI layer + map scene). The mocked GauntletMapEventVisual (see
            // MockMapEventVisual) is constructor-skipped and NREs inside this method, so suppress it: this
            // is the "UI functionality is mocked" boundary for map events.
            AccessTools.Method(typeof(GauntletMapEventVisual), nameof(GauntletMapEventVisual.Initialize)),
        };
    }

    public void Dispose() => TestEnvironment.Dispose();

    /// <summary>
    /// Creates and registers a <see cref="MapEvent"/> on the server with two <see cref="MobileParty"/>
    /// participants, returning the IDs of the map event, attacker party, and defender party.
    /// </summary>
    protected MapEventContext CreateServerMapEvent()
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
            mapEvent.MapEventVisual = MockMapEventVisual();

            // Construction has already replicated the MapEvent to the clients, where the real
            // MapEventRegistry.OnClientCreated runs and allocates their _sides array. The synchronous
            // MapEventSideAssigned replication produced by Initialize therefore lands on a non-null array.
            mapEvent.Initialize(attackerParty.Party, defenderParty.Party);

            Assert.True(Server.ObjectManager.TryGetId(mapEvent, out mapEventId));
            Assert.True(Server.ObjectManager.TryGetId(attackerParty, out attackerPartyId));
            Assert.True(Server.ObjectManager.TryGetId(defenderParty, out defenderPartyId));
        }, MapEventDisabledMethods);

        Assert.NotNull(mapEventId);
        Assert.NotNull(attackerPartyId);
        Assert.NotNull(defenderPartyId);

        return new MapEventContext(
            mapEventId,
            attackerPartyId,
            defenderPartyId
        );
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

            // Finish through FinishBattle (not FinalizeEvent): FinishBattle is the method the AutoRegistry
            // watches as the MapEvent destroy hook (MapEventRegistry.DestroyMethods), and it performs the same
            // finalization work internally (FinishBattle -> FinalizeEventAux). It must NOT run inside an
            // AllowedThread: LifetimePatches.DestroyPostfix only broadcasts the removal to clients when the
            // call is *not* an allowed/original-policy call, so wrapping it would silently skip the sync.
            mapEvent.FinishBattle();
        }, MapEventDisabledMethods);
    }

    // ------------------------------------------------------------------
    // UI / Context mocking
    // ------------------------------------------------------------------

    /// <summary>
    /// Creates a <see cref="GauntletMapEventVisual"/> without running its constructor. The real visual
    /// requires a render context (GauntletUI layer + MapState) that does not exist in the headless test
    /// environment, so every MapEvent in tests is given this stand-in. The UI logic that would normally
    /// run against it (e.g. <c>MapEventVisualsVM.UpdateMapEventsAux</c>) is wrapped in a Harmony finalizer
    /// in production (<c>MapEventRobustnessPatches</c>) so it is safe to leave un-initialized here.
    /// </summary>
    protected static GauntletMapEventVisual MockMapEventVisual()
        => ObjectHelper.SkipConstructor<GauntletMapEventVisual>();

    // ------------------------------------------------------------------
    // Parties joining map events
    // ------------------------------------------------------------------

    /// <summary>
    /// Creates a real, fully-initialized <see cref="MapEvent"/> on the server and returns the string id of
    /// its attacker <see cref="MapEventSide"/>. The map event and its sides are propagated to all clients;
    /// parties can then reinforce that side with <see cref="JoinPartyToSide"/>.
    /// </summary>
    /// <remarks>
    /// The side is taken from an <c>Initialize</c>d map event (not a stand-alone <see cref="MapEventSide"/>)
    /// so that <c>AddPartyInternal</c> runs against a side whose parent <see cref="MapEvent"/> has a populated
    /// <c>_sides</c> array. This lets the vanilla reinforcement path
    /// (<c>MapEventSide.AddPartyInternal</c> → <c>MapEvent.AddInvolvedPartyInternal</c> →
    /// <c>RecalculateRenownAndInfluenceValuesOnPartyInvolved</c>) execute without suppression.
    /// </remarks>
    protected string CreateServerMapEventSide()
    {
        var mapEvent = CreateServerMapEvent();

        string? sideId = null;
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEvent.MapEventId, out var me));
            Assert.True(Server.ObjectManager.TryGetId(me.AttackerSide, out sideId));
        }, MapEventDisabledMethods);

        Assert.NotNull(sideId);
        return sideId!;
    }

    /// <summary>
    /// Creates a new <see cref="MobileParty"/> on the server and joins it to an existing battle
    /// <see cref="MapEventSide"/>, mirroring a party reinforcing that side of a battle. The created
    /// <see cref="MapEventParty"/> and the side membership are propagated to all clients.
    /// </summary>
    /// <param name="mapEventSideId">String id of a side created via <see cref="CreateServerMapEventSide"/>.</param>
    /// <returns>The string id of the <see cref="MapEventParty"/> that joined the side.</returns>
    protected string JoinPartyToSide(string mapEventSideId)
    {
        string? mapEventPartyId = null;

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEventSide>(mapEventSideId, out var side));

            var party = GameObjectCreator.CreateInitializedObject<MobileParty>();

            // Assigning PartyBase.MapEventSide is the vanilla reinforcement entry point: the setter wires the
            // party's side first (so PartyBase.Side is valid for the renown recalculation) and then calls
            // MapEventSide.AddPartyInternal internally. Calling AddPartyInternal directly skips that wiring,
            // leaving Side == None (-1) and throwing IndexOutOfRange in
            // RecalculateRenownAndInfluenceValuesOnPartyInvolved. It is NOT wrapped in AllowedThread: the
            // collection-add must go through the patched MBList.Add so the server broadcasts the new party to
            // the clients (MapEventSideCollectionPatches.ListAddOverride).
            party.Party.MapEventSide = side;

            var joined = side.Parties.LastOrDefault(p => p?.Party == party.Party);
            Assert.NotNull(joined);
            Assert.True(Server.ObjectManager.TryGetId(joined, out mapEventPartyId));
        }, MapEventDisabledMethods);

        Assert.NotNull(mapEventPartyId);
        return mapEventPartyId!;
    }

    /// <summary>
    /// Asserts that the <see cref="MapEventParty"/> with <paramref name="mapEventPartyId"/> is part of the
    /// side with <paramref name="mapEventSideId"/> on the supplied <paramref name="instance"/>.
    /// </summary>
    protected void AssertPartyInSide(EnvironmentInstance instance, string mapEventSideId, string mapEventPartyId)
    {
        Assert.True(instance.ObjectManager.TryGetObject<MapEventSide>(mapEventSideId, out var side));
        Assert.True(instance.ObjectManager.TryGetObject<MapEventParty>(mapEventPartyId, out var mapEventParty));

        Assert.Contains(mapEventParty, side.Parties);
    }

    // ------------------------------------------------------------------
    // Player party / hero designation
    // ------------------------------------------------------------------

    /// <summary>
    /// Creates a <see cref="Hero"/> and a <see cref="MobileParty"/> that are synced to all instances and
    /// registers them as a player-controlled party in every instance's <see cref="IPlayerManager"/>.
    /// After this call <c>party.IsPlayerParty()</c> returns true everywhere, which drives the player-specific
    /// branches in the MapEvent patches (join windows, captivity, surrender, etc.).
    /// </summary>
    /// <returns>The string ids of the created player hero and party.</returns>
    protected (string heroId, string partyId) CreatePlayerHeroParty(string controllerId)
    {
        var heroId = TestEnvironment.CreateRegisteredObject<Hero>();
        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();

        RegisterAsPlayerParty(controllerId, heroId, partyId);

        return (heroId, partyId);
    }

    /// <summary>
    /// Registers an already-synced hero/party pair as a player in every instance's player registry.
    /// </summary>
    protected void RegisterAsPlayerParty(string controllerId, string heroId, string partyId)
    {
        void Register(EnvironmentInstance instance)
        {
            instance.Call(() =>
            {
                var registry = instance.Resolve<IPlayerManager>();
                registry.AddPlayer(new Player(controllerId, heroId, partyId, "MyClanId", "MyCharacterObjectId"));

                Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
                Assert.True(registry.TryGetPlayer(controllerId, out var _));
            });
        }

        Register(Server);
        foreach (var client in Clients)
        {
            Register(client);
        }
    }

    // ------------------------------------------------------------------
    // PlayerCaptivity
    // ------------------------------------------------------------------

    /// <summary>
    /// Makes the hero with <paramref name="heroId"/> a prisoner of the party with
    /// <paramref name="captorPartyId"/> on the server. <see cref="Hero.PartyBelongedToAsPrisoner"/> is an
    /// AutoSynced property, so this propagates the captivity state to every client.
    /// </summary>
    protected void StartCaptivity(string heroId, string captorPartyId)
    {
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(captorPartyId, out var captor));

            // Do NOT wrap in AllowedThread: the AutoSynced property broadcasts through its patched setter,
            // and AllowedThread signals "run the original setter without re-broadcasting" (used by the
            // receiving side). The server change must go through the intercept so it replicates to clients.
            hero.PartyBelongedToAsPrisoner = captor.Party;

            Assert.Equal(captor.Party, hero.PartyBelongedToAsPrisoner);
        }, MapEventDisabledMethods);
    }

    /// <summary>
    /// Releases the hero with <paramref name="heroId"/> from captivity on the server, syncing the cleared
    /// state to all clients.
    /// </summary>
    protected void EndCaptivity(string heroId)
    {
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(heroId, out var hero));

            hero.PartyBelongedToAsPrisoner = null;

            Assert.Null(hero.PartyBelongedToAsPrisoner);
        }, MapEventDisabledMethods);
    }

    /// <summary>
    /// Asserts that the hero with <paramref name="heroId"/> is a prisoner of the party with
    /// <paramref name="captorPartyId"/> (or, when <paramref name="captorPartyId"/> is null, that the hero
    /// is free) on the given <paramref name="instance"/>.
    /// </summary>
    protected void AssertCaptivity(EnvironmentInstance instance, string heroId, string? captorPartyId)
    {
        Assert.True(instance.ObjectManager.TryGetObject<Hero>(heroId, out var hero));

        if (captorPartyId == null)
        {
            Assert.Null(hero.PartyBelongedToAsPrisoner);
            return;
        }

        Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(captorPartyId, out var captor));
        Assert.Equal(captor.Party, hero.PartyBelongedToAsPrisoner);
    }

    /// <summary>
    /// Simulates the player party with <paramref name="playerPartyId"/> losing a battle. Builds a real
    /// <see cref="MapEvent"/> with <paramref name="captorPartyId"/> on the winning side and the player party on
    /// the losing side, then runs the real <see cref="MapEvent.CaptureDefeatedPartyMembers"/> on the server.
    /// </summary>
    /// <remarks>
    /// This is the path that takes a defeated client captive. The player hero is deliberately NOT made the
    /// party's <c>LeaderHero</c> (on the server a player party's <c>PartyComponent.Leader</c> is not reliably
    /// set), so the coop capture must resolve the captured hero from the player registry — which is what
    /// <c>PlayerStartCaptivityPatches</c> does in a <em>prefix</em> (before native removes the leader and
    /// scatters members). The native <c>TakePrisonerAction.ApplyInternal</c> mutation is suppressed so the
    /// assertion deterministically exercises the coop capture/sync path (<c>PrisonerTaken</c> →
    /// <c>PlayerCaptivityHandler</c> → AutoSynced <see cref="Hero.PartyBelongedToAsPrisoner"/>), not vanilla
    /// prisoner RNG.
    /// </remarks>
    protected void DefeatPlayerPartyInBattle(string playerHeroId, string playerPartyId, string captorPartyId)
    {
        var disabledMethods = MapEventDisabledMethods
            // Native's capture-chance model indexes _sides by WinningSide, which is invalid here because no
            // battle result was committed (we drive the capture directly). The coop prefix clears the defeated
            // player party's roster before native's loop, so these (now unused) chances are never dereferenced.
            .Append(AccessTools.Method(typeof(DefaultBattleRewardModel), nameof(DefaultBattleRewardModel.GetCaptureMemberChancesForWinnerParties)))
            .ToList();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(playerHeroId, out var playerHero));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(playerPartyId, out var playerParty));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(captorPartyId, out var captorParty));

            // Make the player hero a member of (not leader of) the player party: the capture resolves the hero
            // from the player registry, and PlayerCaptivityHandler.Handle_PrisonerTaken derives the party from
            // hero.PartyBelongedTo. This is local server setup; wrap in AllowedThread so it does not re-broadcast.
            using (new AllowedThread())
            {
                playerParty.MemberRoster.AddToCounts(playerHero.CharacterObject, 1);
                playerHero.PartyBelongedTo = playerParty;
            }

            // attacker = captor (winner), defender = player party (loser)
            var mapEvent = GameObjectCreator.CreateInitializedObject<MapEvent>();
            mapEvent.MapEventVisual = MockMapEventVisual();
            mapEvent.Initialize(captorParty.Party, playerParty.Party);

            mapEvent.CaptureDefeatedPartyMembers(mapEvent.AttackerSide.Parties, mapEvent.DefenderSide.Parties);
        }, disabledMethods);
    }

    // ------------------------------------------------------------------
    // PlayerEncounter
    // ------------------------------------------------------------------

    /// <summary>
    /// Installs a mocked <see cref="PlayerEncounter"/> as <c>PlayerEncounter.Current</c> on the given
    /// <paramref name="instance"/>. <see cref="PlayerEncounter"/> is a per-campaign engine singleton whose
    /// real construction (<c>PlayerEncounter.Start</c>/<c>Init</c>) requires a rendered encounter menu and a
    /// fully loaded campaign, neither of which exist headlessly. This stand-in lets code paths that consult
    /// <c>PlayerEncounter.Current</c> (battle start gating, captivity, surrender) run against a controllable
    /// context. The encounter is local state and is not synchronized between instances.
    /// </summary>
    /// <param name="instance">Instance (server or client) to install the encounter on.</param>
    /// <param name="encounteredPartyId">Optional party the player is encountering.</param>
    /// <param name="mapEventId">Optional map event attached to the encounter.</param>
    /// <returns>The mocked <see cref="PlayerEncounter"/>.</returns>
    protected PlayerEncounter SetMockPlayerEncounter(EnvironmentInstance instance, string? encounteredPartyId = null, string? mapEventId = null)
    {
        PlayerEncounter? encounter = null;

        instance.Call(() =>
        {
            encounter = ObjectHelper.SkipConstructor<PlayerEncounter>();

            if (encounteredPartyId != null)
            {
                Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(encounteredPartyId, out var encountered));
                encounter._encounteredParty = encountered.Party;
            }

            if (mapEventId != null)
            {
                Assert.True(instance.ObjectManager.TryGetObject<MapEvent>(mapEventId, out var mapEvent));
                encounter._mapEvent = mapEvent;
            }

            Campaign.Current.PlayerEncounter = encounter;

            // PlayerEncounter.Current resolves to Campaign.Current.PlayerEncounter; verify the mock is live.
            Assert.Same(encounter, PlayerEncounter.Current);
        }, MapEventDisabledMethods);

        Assert.NotNull(encounter);
        return encounter!;
    }

    /// <summary>
    /// Clears <c>PlayerEncounter.Current</c> on the given <paramref name="instance"/>, simulating the player
    /// leaving an encounter.
    /// </summary>
    protected void ClearPlayerEncounter(EnvironmentInstance instance)
    {
        instance.Call(() =>
        {
            Campaign.Current.PlayerEncounter = null;
            Assert.Null(PlayerEncounter.Current);
        }, MapEventDisabledMethods);
    }

    /// <summary>
    /// Asserts whether a <c>PlayerEncounter.Current</c> is active on the given <paramref name="instance"/>.
    /// </summary>
    protected void AssertHasPlayerEncounter(EnvironmentInstance instance, bool expected)
    {
        instance.Call(() =>
        {
            if (expected)
                Assert.NotNull(PlayerEncounter.Current);
            else
                Assert.Null(PlayerEncounter.Current);
        }, MapEventDisabledMethods);
    }
}
