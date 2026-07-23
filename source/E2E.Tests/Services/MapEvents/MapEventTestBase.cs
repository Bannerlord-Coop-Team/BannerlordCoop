using Common.Network;
using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.PlayerCaptivityService.Messages;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using HarmonyLib;
using Helpers;
using Moq;
using SandBox.GauntletUI.Map;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
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
    protected MapEventContext CreateServerMapEvent(bool commit = true)
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
            mapEvent.Initialize(
                attackerParty.Party,
                defenderParty.Party,
                new FieldBattleEventComponent(mapEvent),
                MapEvent.BattleTypes.FieldBattle);
            mapEvent.MapEventVisual = null;

            if (commit && !Campaign.Current.MapEventManager.MapEvents.Contains(mapEvent))
                Campaign.Current.MapEventManager.OnMapEventCreated(mapEvent);

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

            // Finalize the way the live server does through FinalizeEvent, the public path battles take on
            // the host. FinalizeEvent funnels into the patched FinalizeEventAux, which broadcasts removal
            // after vanilla teardown completes. It must not run inside an AllowedThread because that makes
            // the patches stand down and skips the sync.
            mapEvent.FinalizeEvent();
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
            // MapEventSidePatches.AddIntercept broadcasts this collection add to clients.
            party.Party.MapEventSide = side;

            var joined = side.Parties.LastOrDefault(p => p?.Party == party.Party);
            Assert.NotNull(joined);
            Assert.True(Server.ObjectManager.TryGetId(joined, out mapEventPartyId));
        }, MapEventDisabledMethods);

        Assert.NotNull(mapEventPartyId);
        return mapEventPartyId!;
    }

    /// <summary>
    /// Creates a new <see cref="MobileParty"/> on the server and joins it to the given side of an existing
    /// map event through the vanilla reinforcement entry point (see <see cref="JoinPartyToSide"/> for why
    /// the <see cref="PartyBase.MapEventSide"/> setter is the correct join path). Returns the MOBILE
    /// PARTY's id — usable with <see cref="SeedPartyTroopOnAll"/> — unlike <see cref="JoinPartyToSide"/>,
    /// which returns the <see cref="MapEventParty"/> id.
    /// </summary>
    protected string JoinNewServerPartyToSide(string mapEventId, BattleSideEnum side)
    {
        string? partyId = null;
        var disabledMethods = MapEventDisabledMethods
            // Synthetic party visibility has no live campaign feat model.
            .Append(AccessTools.Method(typeof(PartyBaseHelper), nameof(PartyBaseHelper.HasFeat)))
            .ToList();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventId, out var mapEvent));

            var party = GameObjectCreator.CreateInitializedObject<MobileParty>();
            party.Party.MapEventSide = mapEvent.GetMapEventSide(side);

            Assert.True(Server.ObjectManager.TryGetId(party, out partyId));
        }, disabledMethods);

        Assert.NotNull(partyId);
        return partyId!;
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
    /// scatters members). Native <c>TakePrisonerAction.ApplyInternal</c> runs with patches live, so each of
    /// its side effects replicates to the clients as it happens (roster deltas + the AutoSynced
    /// <see cref="Hero.PartyBelongedToAsPrisoner"/>), and the postfix-published <c>PrisonerTaken</c> drives
    /// the coop park (<c>PlayerCaptivityServerHandler</c>).
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
            // from the player registry, and TakePrisonerActionPatches reads hero.PartyBelongedTo to snapshot the
            // captured party for the PrisonerTaken message. AddToCounts runs under AllowedThread but STILL
            // replicates (TroopRosterAddToCountsPatch publishes for AddToCounts even on an allowed thread), so
            // every client's roster also gains the hero — the convergence the capture's index-based removal
            // delta relies on. Only the PartyBelongedTo write stays local.
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

    /// <summary>
    /// Simulates the player losing a battle through the <em>real</em> result path: a client resolves the
    /// battle and sets <see cref="MapEvent.BattleState"/> to a victory for the captor's side, which syncs to
    /// the server (<c>MapEventHandler.Handle_NetworkChangeBattleState</c>) and there runs
    /// <c>OnBattleWon</c> → <c>CalculateAndCommitMapEventResults</c> → <c>CaptureDefeatedPartyMembers</c> →
    /// the coop capture. This is the route the live game uses (unlike <see cref="DefeatPlayerPartyInBattle"/>,
    /// which calls <c>CaptureDefeatedPartyMembers</c> directly); it specifically guards against the server
    /// applying the battle state inside an <c>AllowedThread</c>, which would bypass the coop capture/sync.
    /// </summary>
    /// <remarks>
    /// The captor is the attacker (winner) and the player party the defender (loser). The loot/result-commit
    /// steps of <c>CalculateAndCommitMapEventResults</c> need a live campaign world, so they are disabled —
    /// only the capture step is exercised.
    /// </remarks>
    protected void DefeatPlayerByBattleStateSync(string playerHeroId, string playerPartyId, string captorPartyId)
    {
        string? mapEventId = null;

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(playerHeroId, out var playerHero));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(playerPartyId, out var playerParty));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(captorPartyId, out var captorParty));

            using (new AllowedThread())
            {
                playerParty.MemberRoster.AddToCounts(playerHero.CharacterObject, 1);
                playerHero.PartyBelongedTo = playerParty;
            }

            // attacker = captor (winner), defender = player party (loser). Construction replicates the
            // MapEvent (and its sides) to the clients, so the client below can resolve and finish it.
            var mapEvent = GameObjectCreator.CreateInitializedObject<MapEvent>();
            mapEvent.MapEventVisual = MockMapEventVisual();
            mapEvent.Initialize(captorParty.Party, playerParty.Party);

            Assert.True(Server.ObjectManager.TryGetId(mapEvent, out mapEventId));
        }, MapEventDisabledMethods);

        Assert.NotNull(mapEventId);

        var disabledMethods = MapEventDisabledMethods
            .Append(AccessTools.Method(typeof(DefaultBattleRewardModel), nameof(DefaultBattleRewardModel.GetCaptureMemberChancesForWinnerParties)))
            .Append(AccessTools.Method(typeof(MapEvent), "LootDefeatedPartyCasualties"))
            .Append(AccessTools.Method(typeof(MapEvent), "LootDefeatedPartyItems"))
            .Append(AccessTools.Method(typeof(MapEvent), "LootDefeatedPartyPrisoners"))
            .Append(AccessTools.Method(typeof(MapEvent), "LootDefeatedPartyShips"))
            .Append(AccessTools.Method(typeof(MapEvent), "CalculateMapEventResults"))
            .Append(AccessTools.Method(typeof(MapEvent), "CommitCalculatedMapEventResults"))
            .ToList();

        // The client that fought the battle commits the result. Setting BattleState publishes the change,
        // which the server applies authoritatively — capturing the defeated player there.
        var client = Clients.First();
        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<MapEvent>(mapEventId!, out var clientMapEvent));
            clientMapEvent.BattleState = BattleState.AttackerVictory;
        }, disabledMethods);
    }

    /// <summary>
    /// Simulates one or more ALLIED players losing a battle to <paramref name="captorPartyId"/> through the
    /// <em>real</em> result path: the losers share the defender side, a client commits the captor's victory
    /// <see cref="BattleState.AttackerVictory"/>, and the server applies it authoritatively — capturing every
    /// defeated player there (the coop capture resolves each from the player registry in a prefix, before native
    /// removes the side leaders). <see cref="DefeatPlayerByBattleStateSync"/> is the one-loser case.
    /// </summary>
    protected void DefeatAlliedPlayersByBattleStateSync(string captorPartyId, bool playersAreAttackers, params (string heroId, string partyId)[] players)
    {
        Assert.NotEmpty(players);

        string? mapEventId = null;
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(captorPartyId, out var captorParty));

            var loserParties = new MobileParty[players.Length];
            using (new AllowedThread())
            {
                for (int i = 0; i < players.Length; i++)
                {
                    Assert.True(Server.ObjectManager.TryGetObject<Hero>(players[i].heroId, out var hero));
                    Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(players[i].partyId, out var party));
                    party.MemberRoster.AddToCounts(hero.CharacterObject, 1);
                    hero.PartyBelongedTo = party;
                    loserParties[i] = party;
                }
            }

            // The losing players share one side; the captor (winner) holds the other. The first loser is wired via
            // Initialize, the rest joined onto the same side through the PartyBase.MapEventSide setter (the vanilla
            // reinforcement entry point, which wires Side before the renown recalculation).
            var mapEvent = GameObjectCreator.CreateInitializedObject<MapEvent>();
            mapEvent.MapEventVisual = MockMapEventVisual();
            if (playersAreAttackers)
                mapEvent.Initialize(loserParties[0].Party, captorParty.Party);
            else
                mapEvent.Initialize(captorParty.Party, loserParties[0].Party);

            var losingSide = playersAreAttackers ? mapEvent.AttackerSide : mapEvent.DefenderSide;
            for (int i = 1; i < loserParties.Length; i++)
                loserParties[i].Party.MapEventSide = losingSide;

            Assert.True(Server.ObjectManager.TryGetId(mapEvent, out mapEventId));
        }, MapEventDisabledMethods);

        Assert.NotNull(mapEventId);

        var disabledMethods = MapEventDisabledMethods
            .Append(AccessTools.Method(typeof(DefaultBattleRewardModel), nameof(DefaultBattleRewardModel.GetCaptureMemberChancesForWinnerParties)))
            .Append(AccessTools.Method(typeof(MapEvent), "LootDefeatedPartyCasualties"))
            .Append(AccessTools.Method(typeof(MapEvent), "LootDefeatedPartyItems"))
            .Append(AccessTools.Method(typeof(MapEvent), "LootDefeatedPartyPrisoners"))
            .Append(AccessTools.Method(typeof(MapEvent), "LootDefeatedPartyShips"))
            .Append(AccessTools.Method(typeof(MapEvent), "CalculateMapEventResults"))
            .Append(AccessTools.Method(typeof(MapEvent), "CommitCalculatedMapEventResults"))
            .ToList();

        // The winning side is the captor's — the opposite of the players'.
        var winningState = playersAreAttackers ? BattleState.DefenderVictory : BattleState.AttackerVictory;
        var client = Clients.First();
        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<MapEvent>(mapEventId!, out var clientMapEvent));
            clientMapEvent.BattleState = winningState;
        }, disabledMethods);
    }

    /// <summary>
    /// Frees the captive player hero the way the live escape pop-up ("you were able to get away")
    /// does: the owning client's <see cref="EndCaptivityAction"/> is intercepted locally and forwarded
    /// as a <c>NetworkEndPlayerCaptivityAttempted</c> request, which the server applies
    /// authoritatively (<c>PlayerCaptivityServerHandler</c>) and replicates back. The wire message is
    /// sent directly from <paramref name="client"/> because the client-side intercept path resolves
    /// the test hero against <see cref="Hero.MainHero"/> and reads <see cref="MobileParty.MainParty"/>
    /// — the harness's main hero (E2ETestEnvironment.SetupMainHero) is a separate bootstrap hero, and
    /// no main party exists headlessly — so the local handler chain cannot fire for a test hero.
    /// </summary>
    protected void ReleasePlayerByEscapeRequest(EnvironmentInstance client, string heroId, string partyId)
    {
        var disabledMethods = MapEventDisabledMethods
            // An escape releases from a still-active captor, so the server disengages the freed party
            // (TeleportPartyToOutSideOfEncounterRadius), which pathfinds for a reachable point and
            // needs a live map scene. The captor-defeated release path skips it (inactive captor).
            .Append(AccessTools.Method(typeof(MobileParty), nameof(MobileParty.TeleportPartyToOutSideOfEncounterRadius)))
            .ToList();

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));

            client.Resolve<INetwork>().SendAll(new NetworkEndPlayerCaptivityAttempted(
                heroId, partyId, party.Position, EndCaptivityDetail.ReleasedAfterEscape, null, 0));
        }, disabledMethods);
    }

    /// <summary>
    /// Seeds <paramref name="count"/> of <paramref name="characterId"/> into the member roster of the
    /// party with <paramref name="partyId"/> on the server and every client without triggering sync,
    /// so each instance starts from the same known state.
    /// </summary>
    protected void SeedPartyTroopOnAll(string partyId, string characterId, int count)
    {
        void Seed(EnvironmentInstance instance)
        {
            instance.Call(() =>
            {
                using (new AllowedThread())
                {
                    Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
                    Assert.True(instance.ObjectManager.TryGetObject<CharacterObject>(characterId, out var character));

                    // Seed via AddNewElement + AddToCountsAtIndex, which AllowedThread suppresses.
                    // AddToCounts must not be used here: TroopRosterAddToCountsPatch intentionally
                    // publishes sync for AddToCounts even on an allowed thread, so seeding through it
                    // would sync the seed too.
                    party.MemberRoster.AddNewElement(character, -1);
                    var index = party.MemberRoster.FindIndexOfTroop(character);
                    party.MemberRoster.AddToCountsAtIndex(index, count, woundedCountChange: 0, xpChange: 0, removeDepleted: false);
                }
            });
        }

        Seed(Server);
        foreach (var client in Clients)
        {
            Seed(client);
        }
    }

    /// <summary>
    /// Asserts the member roster of the party with <paramref name="partyId"/> holds exactly
    /// <paramref name="expected"/> men on the given <paramref name="instance"/>.
    /// <see cref="TroopRoster.TotalManCount"/> is what every nameplate/speed/wage computation reads,
    /// so a stale or duplicated element shows up here even when the roster still "contains" the hero.
    /// </summary>
    protected void AssertPartyManCount(EnvironmentInstance instance, string partyId, int expected)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));

            Assert.True(
                expected == party.MemberRoster.TotalManCount,
                $"[{instance.GetType().Name}] party {partyId} should have {expected} men, has {party.MemberRoster.TotalManCount}");
        });
    }

    /// <summary>
    /// Asserts the prison roster of the party with <paramref name="partyId"/> holds exactly
    /// <paramref name="expected"/> prisoners on the given <paramref name="instance"/>. Guards the
    /// captor's side of a capture: the prisoner must be counted once everywhere — a replicated add
    /// applied on top of a locally derived one shows up here as a doubled count.
    /// </summary>
    protected void AssertPartyPrisonerCount(EnvironmentInstance instance, string partyId, int expected)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));

            Assert.True(
                expected == party.PrisonRoster.TotalManCount,
                $"[{instance.GetType().Name}] party {partyId} should have {expected} prisoners, has {party.PrisonRoster.TotalManCount}");
        });
    }

    /// <summary>
    /// Reads the member-roster man count of the party on the given instance. Baseline helper: harness
    /// parties spawn with nondeterministic rosters, so tests assert relative to a snapshot, never absolute.
    /// </summary>
    protected int GetPartyManCount(EnvironmentInstance instance, string partyId)
    {
        int count = 0;
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            count = party.MemberRoster.TotalManCount;
        });
        return count;
    }

    /// <summary>
    /// Reads the member-roster man count of NON-hero elements only (see <see cref="GetPartyManCount"/>).
    /// The capture/surrender transfer moves regular troops to the captor but never raw hero elements
    /// (heroes are captured individually via TakePrisonerAction), so prisoner expectations build on this.
    /// </summary>
    protected int GetPartyNonHeroManCount(EnvironmentInstance instance, string partyId)
    {
        int count = 0;
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            for (int i = 0; i < party.MemberRoster.Count; i++)
            {
                var element = party.MemberRoster.GetElementCopyAtIndex(i);
                if (element.Character?.IsHero != true)
                    count += Math.Max(element.Number, 0);
            }
        });
        return count;
    }

    /// <summary>
    /// Counts the live (alive, non-prisoner, non-depleted) hero elements in the member roster of the party
    /// with <paramref name="partyId"/> — the heroes a capture takes prisoner individually via
    /// <c>TakePrisonerAction</c>: the registered player hero through the native defeat path and every other
    /// rider through the companion capture (BR-061). The harness lord party spawns with its own bootstrap
    /// lord hero riding in the roster, so hero expectations must be counted, never hard-coded.
    /// </summary>
    protected int GetPartyLiveHeroCount(EnvironmentInstance instance, string partyId)
    {
        int count = 0;
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            for (int i = 0; i < party.MemberRoster.Count; i++)
            {
                var element = party.MemberRoster.GetElementCopyAtIndex(i);
                if (element.Character?.IsHero != true || element.Number <= 0) continue;

                var hero = element.Character.HeroObject;
                if (hero?.IsAlive == true && !hero.IsPrisoner)
                    count++;
            }
        });
        return count;
    }

    /// <summary>
    /// Counts the capturable hero elements in the member roster of the party with <paramref name="partyId"/> —
    /// like <see cref="GetPartyLiveHeroCount"/>, but ALSO excludes heroes carrying a battle
    /// <see cref="Hero.DeathMark"/> (<c>DiedInBattle</c> / <c>DiedInLabor</c>). During an active map event
    /// native <see cref="KillCharacterAction"/> defers the kill and only records a DeathMark, so a hero killed
    /// in the current battle still reports <see cref="Hero.IsAlive"/> == true; those heroes are NOT taken
    /// prisoner (matching native <c>MapEvent.CaptureDefeatedPartyMembers</c>), so a prisoner expectation must
    /// exclude them — which <see cref="GetPartyLiveHeroCount"/> (aliveness only) would over-count.
    /// </summary>
    protected int GetPartyCapturableHeroCount(EnvironmentInstance instance, string partyId)
    {
        int count = 0;
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            for (int i = 0; i < party.MemberRoster.Count; i++)
            {
                var element = party.MemberRoster.GetElementCopyAtIndex(i);
                if (element.Character?.IsHero != true || element.Number <= 0) continue;

                var hero = element.Character.HeroObject;
                if (hero == null || !hero.IsAlive || hero.IsPrisoner) continue;
                if (hero.DeathMark == KillCharacterAction.KillCharacterActionDetail.DiedInBattle
                    || hero.DeathMark == KillCharacterAction.KillCharacterActionDetail.DiedInLabor) continue;

                count++;
            }
        });
        return count;
    }

    /// <summary>Reads the prison-roster man count of the party on the given instance (see <see cref="GetPartyManCount"/>).</summary>
    protected int GetPartyPrisonerCount(EnvironmentInstance instance, string partyId)
    {
        int count = 0;
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            count = party.PrisonRoster.TotalManCount;
        });
        return count;
    }

    /// <summary>
    /// Frees the player hero the way native does when its captor party is defeated in battle:
    /// <see cref="MapEvent.LootDefeatedPartyPrisoners"/> calls
    /// <see cref="EndCaptivityAction.ApplyByReleasedAfterBattle"/> for each freed prisoner. Invoking that
    /// exact entry point on the server exercises the coop release path
    /// (<c>EndCaptivityActionPatches</c> → <c>PlayerCaptivityEndedByServer</c> →
    /// <c>PlayerCaptivityServerHandler</c>) deterministically, without the faction/RNG of a full battle
    /// resolution.
    /// </summary>
    protected void ReleasePlayerAfterCaptorDefeated(string playerHeroId)
    {
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(playerHeroId, out var playerHero));

            // A captor defeated in battle is destroyed/deactivated; reflect that so the release follows the
            // defeated-captor path (no attempt to disengage from a still-active captor).
            var captor = playerHero.PartyBelongedToAsPrisoner?.MobileParty;
            if (captor != null)
            {
                using (new AllowedThread())
                {
                    captor.IsActive = false;
                }
            }

            EndCaptivityAction.ApplyByReleasedAfterBattle(playerHero);
        }, MapEventDisabledMethods);
    }

    /// <summary>
    /// Asserts the player party with <paramref name="partyId"/> is active again and holds exactly its
    /// hero <paramref name="heroId"/> — i.e. it was restored to the map after a captivity release.
    /// Captivity forfeits the party's troops, so the restored roster is exactly one man; a stale
    /// element surviving the capture would double-count here.
    /// </summary>
    protected void AssertPlayerPartyRestored(EnvironmentInstance instance, string heroId, string partyId)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));

            Assert.True(party.IsActive, $"Player party {partyId} should be active again after release on {instance.GetType().Name}");
        });

        AssertHeroInPartyRoster(instance, heroId, partyId);
    }

    /// <summary>
    /// Asserts the released hero <paramref name="heroId"/> is back in its party <paramref name="partyId"/>'s
    /// member roster on the given <paramref name="instance"/>, and that the roster holds exactly that one
    /// man (party activation is not asserted, so this is safe to use on clients where
    /// <see cref="MobileParty.IsActive"/> is not synced). Contains guards the "released party has 0 troops"
    /// bug — the server's re-add must replicate; the exact count guards the inverse "phantom troop" bug —
    /// a stale hero element surviving the capture on a client doubles the count when the re-add lands.
    /// </summary>
    protected void AssertHeroInPartyRoster(EnvironmentInstance instance, string heroId, string partyId)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
            Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));

            Assert.True(
                party.MemberRoster.Contains(hero.CharacterObject),
                $"Released hero {heroId} should be back in party {partyId} on {instance.GetType().Name} (count={party.MemberRoster.TotalManCount})");
            Assert.True(
                party.MemberRoster.TotalManCount == 1,
                $"Released party {partyId} should hold exactly its hero on {instance.GetType().Name}, has {party.MemberRoster.TotalManCount} men");
        });
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

    /// <summary>
    /// Gives <paramref name="instance"/> the minimal campaign state <see cref="PlayerEncounter.Finish"/> needs to
    /// run headless. With no menu context open, Finish writes <c>Campaign.Current.MapStateData.GameMenuId</c>,
    /// which NREs when <see cref="MapStateData"/> is unset (the harness Campaign is constructor-skipped). Set it
    /// to a real, empty <see cref="MapStateData"/>. Combined with a set <see cref="MobileParty.MainParty"/> (no
    /// army/settlement/siege) and an encounter with a null map event (so <c>FinalizeBattle</c> no-ops) and no
    /// encountered party (so the result path does not teleport), Finish then runs to completion and clears
    /// <see cref="PlayerEncounter.Current"/>. Finish's own menu exit is guarded by the (null) menu context, but
    /// the surrounding finalize handler's unconditional <c>GameMenu.ExitToLast</c> still needs disabling.
    /// </summary>
    protected void EnableHeadlessEncounterFinish(EnvironmentInstance instance)
    {
        instance.Call(() =>
        {
            if (Campaign.Current.MapStateData == null)
                Campaign.Current.MapStateData = new MapStateData();
        }, MapEventDisabledMethods);
    }
}
