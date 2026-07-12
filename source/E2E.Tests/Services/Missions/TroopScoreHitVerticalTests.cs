using Common.Messaging;
using Common.Util;
using GameInterface.Services.MapEventParties.Messages;
using GameInterface.Services.MapEvents.TroopSupply;
using GameInterface.Services.MapEvents.Patches;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

/// <summary>
/// The score-hit end of the battle vertical, for both battle shapes:
/// <list type="bullet">
/// <item>Live (player) battle — the client controlling the troop that was HIT applies the blow, so its
/// <c>CoopAgentOrigin.OnScoreHit</c> is the single reporter; the report travels to the server
/// (<c>NetworkTroopScoreHit</c>) and the accounted <c>ContributionToBattle</c> comes back through the
/// <c>_contributionToBattle</c> autosync. The reporting client applies nothing locally.</item>
/// <item>Simulation — the server runs <c>MapEventParty.OnTroopScoreHit</c> natively (the prefix passes the
/// server through) and the autosync broadcasts the result; clients never report.</item>
/// </list>
/// </summary>
public class TroopScoreHitVerticalTests : MissionTestEnvironment
{
    public TroopScoreHitVerticalTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void ReportedTroopScoreHit_UpdatesContributionEverywhere()
    {
        var (partyId, troopSeed, victimId) = SetupScoredBattleOnServer();

        int serverContribution = 0;

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEventParty>(partyId, out var party));
            var attacker = Server.GetRegisteredObject<CharacterObject>("e2e_attacker");

            int before = party.ContributionToBattle;

            // The blow-applying client reports the hit; the server accounts it.
            Server.Resolve<IMessageBroker>().Publish(this,
                new NetworkTroopScoreHit(partyId, troopSeed, victimId, damage: 30, isFatal: true, isSimulatedHit: false));

            Assert.True(party.ContributionToBattle > before,
                $"Server contribution did not increase (before={before}, after={party.ContributionToBattle})");
            serverContribution = party.ContributionToBattle;

            // The non-hero attacker banked the hit's xp in the authoritative roster.
            foreach (var element in party.Troops)
            {
                if (element.Troop != attacker) continue;
                Assert.True(element.XpGained > 0, "Attacker troop gained no xp from the scored hit");
            }
        });

        AssertClientsConvergedOn(partyId, serverContribution);
    }

    [Fact]
    public void LiveBattleScoreHit_ClientOriginReport_AppliesOnceOnServerAndConverges()
    {
        var (partyId, troopSeed, victimId) = SetupScoredBattleOnServer();

        var reporter = Clients.First();

        // The reporter is the client controlling the troop that was hit: it holds the attacker as a puppet
        // whose CoopAgentOrigin carries the server-minted descriptor. Give it local identities to report with.
        reporter.Call(() =>
        {
            var attacker = reporter.CreateRegisteredObject<CharacterObject>("e2e_attacker");
            attacker.Culture = ObjectHelper.SkipConstructor<CultureObject>();
            reporter.CreateRegisteredObject<CharacterObject>("e2e_victim");
        });

        reporter.Call(() =>
        {
            Assert.True(reporter.ObjectManager.TryGetObject<MapEventParty>(partyId, out var clientParty));
            var attacker = reporter.GetRegisteredObject<CharacterObject>("e2e_attacker");
            var victim = reporter.GetRegisteredObject<CharacterObject>("e2e_victim");

            // What BattleAgentLogic.OnAgentHit invokes on the client that applied the blow. The replicated
            // battle set PartyBase.MapEventSide (MapEventSideDataHandler), so the origin resolves its party.
            IAgentOriginBase origin = new CoopAgentOrigin(attacker, clientParty.Party, 0, null, new UniqueTroopDescriptor(troopSeed));

            origin.OnScoreHit(victim, null, damage: 30, isFatal: true, isTeamKill: false, attackerWeapon: null);

            // Hits that contribute nothing are not reported at all.
            origin.OnScoreHit(victim, null, damage: 30, isFatal: false, isTeamKill: true, attackerWeapon: null);
            origin.OnScoreHit(victim, null, damage: 0, isFatal: false, isTeamKill: false, attackerWeapon: null);

            // A troop whose party is not in a battle (teardown) reports nothing rather than throwing.
            IAgentOriginBase strayOrigin = new CoopAgentOrigin(attacker, ObjectHelper.SkipConstructor<PartyBase>(), 0, null, new UniqueTroopDescriptor(troopSeed));
            strayOrigin.OnScoreHit(victim, null, damage: 30, isFatal: false, isTeamKill: false, attackerWeapon: null);

            Assert.Equal(1, reporter.InternalMessages.GetMessageCount<OnTroopScoreHitAttempted>());
        });

        // Exactly one report crossed the wire and the server applied it once.
        Assert.Equal(1, Server.InternalMessages.GetMessageCount<NetworkTroopScoreHit>());

        int serverContribution = 0;
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEventParty>(partyId, out var party));
            Assert.True(party.ContributionToBattle > 1, "Server did not account the client-reported hit");
            serverContribution = party.ContributionToBattle;
        });

        // Every client — including the reporter, which applied nothing locally — converges on the server value.
        AssertClientsConvergedOn(partyId, serverContribution);
    }

    [Fact]
    public void SimulatedScoreHit_OnServer_AppliesNativelyAndBroadcasts()
    {
        var (partyId, troopSeed, victimId) = SetupScoredBattleOnServer();

        int serverContribution = 0;

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEventParty>(partyId, out var party));
            var victim = Server.GetRegisteredObject<CharacterObject>("e2e_victim");

            int before = party.ContributionToBattle;

            // The map-battle simulation calls the scorer natively on the server; the prefix passes it through.
            party.OnTroopScoreHit(new UniqueTroopDescriptor(troopSeed), victim, 30, isFatal: true, isTeamKill: false, null, isSimulatedHit: true);

            Assert.True(party.ContributionToBattle > before,
                $"Server contribution did not increase (before={before}, after={party.ContributionToBattle})");
            serverContribution = party.ContributionToBattle;
        });

        AssertClientsConvergedOn(partyId, serverContribution);
    }

    [Fact]
    public void RepeatedScoreHitsInOneTick_SendLatestContributionOnce()
    {
        var (partyId, troopSeed, _) = SetupScoredBattleOnServer();

        Server.NetworkSentMessages.Clear();

        int serverContribution = 0;
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEventParty>(partyId, out var party));
            var victim = Server.GetRegisteredObject<CharacterObject>("e2e_victim");

            party.OnTroopScoreHit(new UniqueTroopDescriptor(troopSeed), victim, 10, isFatal: false, isTeamKill: false, null, isSimulatedHit: true);
            party.OnTroopScoreHit(new UniqueTroopDescriptor(troopSeed), victim, 20, isFatal: false, isTeamKill: false, null, isSimulatedHit: true);
            party.OnTroopScoreHit(new UniqueTroopDescriptor(troopSeed), victim, 30, isFatal: true, isTeamKill: false, null, isSimulatedHit: true);

            serverContribution = party.ContributionToBattle;
        });

        Assert.DoesNotContain(Server.NetworkSentMessages, message => IsContributionMessageFor(message, partyId));

        FlushCoalescer();

        Assert.Single(Server.NetworkSentMessages, message => IsContributionMessageFor(message, partyId));
        AssertClientsConvergedOn(partyId, serverContribution);
    }

    [Fact]
    public void VictoryAndFinalizeBoundaries_FlushPendingContributionBeforeReturning()
    {
        var (partyId, troopSeed, _) = SetupScoredBattleOnServer();

        Server.NetworkSentMessages.Clear();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEventParty>(partyId, out var party));
            var victim = Server.GetRegisteredObject<CharacterObject>("e2e_victim");
            var mapEvent = party.Party.MapEventSide.MapEvent;

            party.OnTroopScoreHit(new UniqueTroopDescriptor(troopSeed), victim, 10, isFatal: false, isTeamKill: false, null, isSimulatedHit: true);
            Assert.DoesNotContain(Server.NetworkSentMessages, message => IsContributionMessageFor(message, partyId));

            var battleStatePrefix = AccessTools.Method(typeof(MapEventPatches), "Prefix_BattleState");
            Assert.True((bool)battleStatePrefix.Invoke(null, new object[] { mapEvent, BattleState.AttackerVictory })!);
            Assert.Single(Server.NetworkSentMessages, message => IsContributionMessageFor(message, partyId));

            party.OnTroopScoreHit(new UniqueTroopDescriptor(troopSeed), victim, 20, isFatal: false, isTeamKill: false, null, isSimulatedHit: true);
            Assert.Single(Server.NetworkSentMessages, message => IsContributionMessageFor(message, partyId));

            var finalizePrefix = AccessTools.Method(typeof(MapEventPatches), "Prefix_FinalizeEventAux");
            Assert.True((bool)finalizePrefix.Invoke(null, new object[] { mapEvent })!);
            Assert.Equal(2, Server.NetworkSentMessages.Count(message => IsContributionMessageFor(message, partyId)));
        });
    }

    private static bool IsContributionMessageFor(IMessage message, string partyId)
    {
        if (message.GetType().Name != "MapEventParty__contributionToBattle_SetNetworkMessage") return false;

        var instanceId = AccessTools.Property(message.GetType(), "InstanceId").GetValue(message) as string;
        return instanceId == ObjectManager.Compact(partyId, typeof(MapEventParty));
    }

    /// <summary>
    /// Stands up a coop battle whose attacker-side party holds one flattened troop, ready to be scored
    /// against: returns the party's id, the troop's roster descriptor seed and the victim character's id
    /// (both characters registered on the server as "e2e_attacker"/"e2e_victim").
    /// </summary>
    private (string partyId, int troopSeed, string victimId) SetupScoredBattleOnServer()
    {
        var (mapEventId, _) = SetupCoopBattle("attacker", "defender");

        EnsureServerCampaignModels();

        string? partyId = null;
        string? victimId = null;
        int troopSeed = 0;

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventId, out var mapEvent));
            var party = mapEvent.AttackerSide.Parties[0];
            Assert.True(Server.ObjectManager.TryGetId(party, out partyId));

            // The attacker troop fights for the party; the victim only needs a coop identity to be resolved
            // by id on the server (the xp model reads its stats off the bare character). The attacker needs
            // a culture — the xp model's perk pass dereferences troop.Culture when the party has a leader.
            var attacker = Server.CreateRegisteredObject<CharacterObject>("e2e_attacker");
            attacker.Culture = ObjectHelper.SkipConstructor<CultureObject>();
            var victim = Server.CreateRegisteredObject<CharacterObject>("e2e_victim");
            Assert.True(Server.ObjectManager.TryGetId(victim, out victimId));

            // Flatten the battle roster the way the engine does; the score hit is keyed on the descriptor
            // seed the roster minted for the troop.
            party.Party.MemberRoster.AddToCounts(attacker, 1);
            party.Update();

            bool found = false;
            foreach (var element in party.Troops)
            {
                if (element.Troop != attacker) continue;
                troopSeed = element.Descriptor.UniqueSeed;
                found = true;
                break;
            }
            Assert.True(found, "Attacker troop missing from the flattened battle roster");
        });

        Assert.NotNull(partyId);
        Assert.NotNull(victimId);

        return (partyId!, troopSeed, victimId!);
    }

    /// <summary>The _contributionToBattle autosync broadcast the server value to every client's copy.</summary>
    private void AssertClientsConvergedOn(string partyId, int serverContribution)
    {
        FlushCoalescer();

        foreach (var client in Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<MapEventParty>(partyId, out var clientParty));
                Assert.Equal(serverContribution, clientParty.ContributionToBattle);
            });
        }
    }

    /// <summary>
    /// The harness boots a <see cref="Campaign"/> without the campaign-start flow that would populate
    /// <c>Campaign.Models</c>, so give the server the models the native scorer runs through
    /// (xp-from-hit → troop power + max hitpoints). Clients never run the scorer, so they need none.
    /// </summary>
    private void EnsureServerCampaignModels()
    {
        Server.Call(() =>
        {
            if (Campaign.Current.Models != null) return;

            var models = new List<GameModel>
            {
                new DefaultCombatXpModel(),
                new DefaultMilitaryPowerModel(),
                new DefaultCharacterStatsModel(),
            };

            var gameModels = Server.GameInstance.Game.AddGameModelsManager<GameModels>(models);
            AccessTools.Field(typeof(Campaign), "_gameModels").SetValue(Campaign.Current, gameModels);
        });
    }
}
