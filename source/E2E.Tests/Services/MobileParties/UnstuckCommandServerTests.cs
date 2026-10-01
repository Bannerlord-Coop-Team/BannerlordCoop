using Common.Commands;
using Common.Util;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Services.MapEvents;
using E2E.Tests.Util;
using GameInterface.Services.Armies.Messages;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Messages.Leave;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.MobileParties.Messages.Unstuck;
using GameInterface.Services.Players;
using HarmonyLib;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MobileParties;

/// <summary>
/// Verifies the server branch of coop.unstuck: the server console resolves a connected player by
/// controller id or hero name and runs the same server unstuck steps as the player's own coop.unstuck.
/// </summary>
public class UnstuckCommandServerTests : MapEventTestBase
{
    private const string CommandName = "coop.unstuck";

    private EnvironmentInstance Client => TestEnvironment.Clients.First();
    private EnvironmentInstance SecondClient => TestEnvironment.Clients.Skip(1).First();

    public UnstuckCommandServerTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void OnClient_WithAPlayer_IsRefusedAndSendsNothing()
    {
        var target = CreateConnectedPlayer("unstuck-target", Client, "Lady Mira");
        StageArmyAndSettlement(target.PartyId);

        CoopCommandResult result = null;
        Client.Call(() => result = Client.Resolve<ICoopCommandRegistry>()
            .ProcessCommand(CommandName, new CoopCommandArgsFactory().FromValues(new[] { "unstuck-target" })));

        Assert.False(result.Succeeded);
        Assert.Equal("invalid_arguments", result.ErrorCode);
        Assert.Equal("On a client coop.unstuck takes no player, it always unsticks your own party. Usage: coop.unstuck", result.Output);
        Assert.Empty(Client.InternalMessages.GetMessages<PlayerUnstuckRequested>());
        Assert.Empty(Client.NetworkSentMessages.GetMessages<NetworkRequestPlayerUnstuck>());
        AssertInArmyAndSettlement(target.PartyId);
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkPlayerUnstuckResult>());
    }

    [Fact]
    public void ByControllerId_ClearsArmyAndSettlement_AndOnlyTheTargetGetsTheResult()
    {
        var target = CreateConnectedPlayer("unstuck-target", Client, "Lady Mira");
        var other = CreateConnectedPlayer("unstuck-other", SecondClient, "Arwa");
        StageArmyAndSettlement(target.PartyId, other.PartyId);

        var result = Execute("unstuck-target");

        Assert.True(result.Succeeded, result.Output);
        Assert.Contains($"Unstuck player unstuck-target (hero Lady Mira, {target.HeroId}):", result.Output);
        Assert.Contains("Removed the party from the army", result.Output);
        Assert.Contains("Removed the party from settlement", result.Output);
        Server.Call(() =>
        {
            var party = Server.GetRegisteredObject<MobileParty>(target.PartyId);
            Assert.Null(party.Army);
            Assert.Null(party.CurrentSettlement);
        });
        AssertInArmyAndSettlement(other.PartyId);

        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkRemovePartyInArmy>());
        Assert.Single(Client.InternalMessages.GetMessages<PlayerUnstuckCompleted>());
        Assert.Empty(SecondClient.InternalMessages.GetMessages<NetworkPlayerUnstuckResult>());

        var repeat = Execute("unstuck-target");
        Assert.True(repeat.Succeeded, repeat.Output);
        Assert.Contains("No server-side stuck state found", repeat.Output);
    }

    [Fact]
    public void ByQuotedHeroName_UnsticksThatPlayer()
    {
        var target = CreateConnectedPlayer("unstuck-target", Client, "Lady Mira");
        CreateConnectedPlayer("unstuck-other", SecondClient, "Arwa");
        StageArmyAndSettlement(target.PartyId);

        Assert.True(new CoopCommandArgsFactory().TryFromConsoleTokens(new[] { "\"lady", "mira\"" }, out var args, out var parseError), parseError);
        CoopCommandResult result = null;
        Server.Call(() => result = Server.Resolve<ICoopCommandRegistry>().ProcessCommand(CommandName, args));

        Assert.True(result.Succeeded, result.Output);
        Assert.Contains("(hero Lady Mira,", result.Output);
        Server.Call(() => Assert.Null(Server.GetRegisteredObject<MobileParty>(target.PartyId).Army));
    }

    [Fact]
    public void UnknownName_FailsListingTheCandidates()
    {
        var target = CreateConnectedPlayer("unstuck-target", Client, "Lady Mira");
        CreateConnectedPlayer("unstuck-other", SecondClient, "Arwa");
        StageArmyAndSettlement(target.PartyId);

        var result = Execute("Derthert");

        Assert.False(result.Succeeded);
        Assert.Contains("unstuck-target (Lady Mira)", result.Output);
        Assert.Contains("unstuck-other (Arwa)", result.Output);
        AssertInArmyAndSettlement(target.PartyId);
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkPlayerUnstuckResult>());
    }

    [Fact]
    public void AmbiguousName_FailsListingTheMatches()
    {
        var target = CreateConnectedPlayer("unstuck-target", Client, "Arwa");
        CreateConnectedPlayer("unstuck-other", SecondClient, "arwa");
        StageArmyAndSettlement(target.PartyId);

        var result = Execute("Arwa");

        Assert.False(result.Succeeded);
        Assert.Contains("Several players are named 'Arwa'", result.Output);
        Assert.Contains("unstuck-target (Arwa)", result.Output);
        Assert.Contains("unstuck-other (arwa)", result.Output);
        AssertInArmyAndSettlement(target.PartyId);
    }

    [Fact]
    public void DisconnectedTarget_IsRefused()
    {
        var target = CreateConnectedPlayer("unstuck-target", Client, "Lady Mira");
        StageArmyAndSettlement(target.PartyId);
        Server.Call(() => Server.Resolve<IPlayerManager>().ClearPeer(Client.NetPeer));

        var result = Execute("unstuck-target");

        Assert.False(result.Succeeded);
        Assert.Contains("is not connected", result.Output);
        AssertInArmyAndSettlement(target.PartyId);
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkPlayerUnstuckResult>());
    }

    [Fact]
    public void TargetPartyMissingOnServer_FailsAndAppliesNothing()
    {
        var target = CreateConnectedPlayer("unstuck-target", Client, "Lady Mira");
        StageArmyAndSettlement(target.PartyId);
        MobileParty party = null;
        Server.Call(() =>
        {
            party = Server.GetRegisteredObject<MobileParty>(target.PartyId);
            Assert.True(Server.ObjectManager.Remove(party));
        });

        var result = Execute("unstuck-target");

        Assert.False(result.Succeeded);
        Assert.Contains($"Party '{target.PartyId}' of player unstuck-target (Lady Mira) was not found on the server; nothing was applied.", result.Output);
        Server.Call(() =>
        {
            Assert.NotNull(party.Army);
            Assert.NotNull(party.CurrentSettlement);
        });
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkPlayerUnstuckResult>());
    }

    [Fact]
    public void CaptiveTarget_ReportsTheReleaseWithTheCaptor()
    {
        var target = CreateConnectedPlayer("unstuck-target", Client, "Lady Mira");
        var captorPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        StartCaptivity(target.HeroId, captorPartyId);
        Server.Call(() =>
        {
            var hero = Server.GetRegisteredObject<Hero>(target.HeroId);
            using (new AllowedThread())
            {
                hero._heroState = Hero.CharacterStates.Prisoner;
            }
        });

        // The escape moves the freed party out of the captor's radius, which needs a live map scene.
        var result = Execute("unstuck-target", MapEventDisabledMethods
            .Append(AccessTools.Method(typeof(MobileParty), nameof(MobileParty.TeleportPartyToOutSideOfEncounterRadius))));

        Assert.True(result.Succeeded, result.Output);
        Server.Call(() =>
        {
            var hero = Server.GetRegisteredObject<Hero>(target.HeroId);
            var captor = Server.GetRegisteredObject<MobileParty>(captorPartyId);
            Assert.Contains($"Applied captivity release (escape) for hero {hero.StringId} from {captor.StringId}.", result.Output);
            Assert.False(hero.IsPrisoner);
        });
        Assert.Single(Client.InternalMessages.GetMessages<PlayerUnstuckCompleted>());
    }

    [Fact]
    public void PlayerLedArmy_IsKept()
    {
        var target = CreateConnectedPlayer("unstuck-target", Client, "Lady Mira");
        var followerId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        string armyId = null;
        Server.Call(() =>
        {
            var leader = Server.GetRegisteredObject<MobileParty>(target.PartyId);
            var kingdom = GameObjectCreator.CreateInitializedObject<Kingdom>();
            var army = new Army(kingdom, leader, Army.ArmyTypes.Patrolling);
            var follower = Server.GetRegisteredObject<MobileParty>(followerId);
            follower.Army = army;
            follower.AttachedTo = leader;
            Assert.True(Server.ObjectManager.TryGetId(army, out armyId));
        }, MapEventDisabledMethods);

        var result = Execute("unstuck-target");

        Assert.True(result.Succeeded, result.Output);
        Assert.Contains("Preserved the player-led army", result.Output);
        Server.Call(() =>
        {
            var army = Server.GetRegisteredObject<Army>(armyId);
            Assert.Same(army, Server.GetRegisteredObject<MobileParty>(target.PartyId).Army);
            Assert.Same(army, Server.GetRegisteredObject<MobileParty>(followerId).Army);
        });
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkRemovePartyInArmy>());
    }

    [Fact]
    public void TargetInHostedBattle_WarnsAndLeavesTheBattle()
    {
        var target = CreateConnectedPlayer("unstuck-target", Client, "Lady Mira");
        var battle = CreateServerMapEvent();
        Server.Call(() =>
        {
            var party = Server.GetRegisteredObject<MobileParty>(target.PartyId);
            party.Party.MapEventSide = Server.GetRegisteredObject<MapEvent>(battle.MapEventId).AttackerSide;
            Server.Resolve<IBattleHostRegistry>().Set(battle.MapEventId, new BattleHostAssignment("unstuck-target", Array.Empty<string>()));
        }, MapEventDisabledMethods);

        var result = Execute("unstuck-target");

        Assert.True(result.Succeeded, result.Output);
        Assert.Contains($"Warning: the party is in hosted battle {battle.MapEventId}", result.Output);
        Assert.Contains("Removed the party from its map event.", result.Output);
        Server.Call(() => Assert.Null(Server.GetRegisteredObject<MobileParty>(target.PartyId).MapEvent));
        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkPartyLeftBattle>());
    }

    private record PlayerIds(string HeroId, string PartyId);

    /// <summary>
    /// Registers a named player on every instance, links it to the client's connection on the server
    /// and makes it that client's main hero and party, so the client applies the unstuck result.
    /// </summary>
    private PlayerIds CreateConnectedPlayer(string controllerId, EnvironmentInstance client, string heroName)
    {
        var ids = CreatePlayerHeroParty(controllerId);
        var characterId = TestEnvironment.CreateRegisteredObject<CharacterObject>();

        Server.Call(() =>
        {
            Server.GetRegisteredObject<Hero>(ids.heroId).SetName(new TextObject(heroName), new TextObject(heroName));
            Server.Resolve<IPlayerManager>().SetPeer(controllerId, client.NetPeer);
        });

        client.Call(() =>
        {
            var hero = client.GetRegisteredObject<Hero>(ids.heroId);
            var character = client.GetRegisteredObject<CharacterObject>(characterId);
            var party = client.GetRegisteredObject<MobileParty>(ids.partyId);
            using (new AllowedThread())
            {
                character.HeroObject = hero;
                hero.PartyBelongedTo = party;
                Game.Current.PlayerTroop = character;
                Campaign.Current.MainParty = party;
            }
        });

        return new PlayerIds(ids.heroId, ids.partyId);
    }

    /// <summary>Puts each party in one AI-led army and one settlement on the server.</summary>
    private void StageArmyAndSettlement(params string[] partyIds)
    {
        var settlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
        var townId = TestEnvironment.CreateRegisteredObject<Town>();
        var kingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var leaderPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();

        Server.Call(() =>
        {
            var settlement = Server.GetRegisteredObject<Settlement>(settlementId);
            var leaderParty = Server.GetRegisteredObject<MobileParty>(leaderPartyId);

            // Created outside AllowedThread so the army registers with the object manager.
            var army = new Army(Server.GetRegisteredObject<Kingdom>(kingdomId), leaderParty, Army.ArmyTypes.Patrolling);

            using (new AllowedThread())
            {
                // The native leave calls SettlementComponent.OnPartyLeft, so wire the town component.
                settlement.SettlementComponent = Server.GetRegisteredObject<Town>(townId);

                // Keep the leader in the army so removing a party does not cascade into a disband.
                if (!army._parties.Contains(leaderParty)) army._parties.Add(leaderParty);

                foreach (var partyId in partyIds)
                {
                    var party = Server.GetRegisteredObject<MobileParty>(partyId);
                    army._parties.Add(party);
                    party._army = army;
                    party.SetCurrentSettlementDirectly(settlement);
                }
            }
        });
    }

    private void AssertInArmyAndSettlement(string partyId)
    {
        Server.Call(() =>
        {
            var party = Server.GetRegisteredObject<MobileParty>(partyId);
            Assert.NotNull(party.Army);
            Assert.NotNull(party.CurrentSettlement);
        });
    }

    private CoopCommandResult Execute(string player, IEnumerable<MethodBase> disabledMethods = null)
    {
        CoopCommandResult result = null;
        Server.Call(() => result = Server.Resolve<ICoopCommandRegistry>()
            .ProcessCommand(CommandName, new CoopCommandArgsFactory().FromValues(new[] { player })), disabledMethods);
        return result;
    }
}
