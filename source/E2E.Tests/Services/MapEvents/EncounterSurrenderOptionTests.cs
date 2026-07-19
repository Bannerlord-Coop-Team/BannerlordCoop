using Common.Util;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Handlers;
using HarmonyLib;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MapEvents;

/// <summary>
/// Availability of the encounter menu's Surrender option on a client, driven through the REAL (patched)
/// <c>EncounterGameMenuBehavior.game_menu_encounter_surrender_on_condition</c>:
/// <list type="bullet">
/// <item>An incapacitated defender — wounded main hero, no healthy member in its own party — is always offered
/// surrender, even while its side still counts healthy troops elsewhere (the client-side lag that natively hides
/// the option and soft-locks the player: attack is blocked by the wound, send-troops by the mode claim, get-away
/// by its troop cost, and a defender has no plain leave).</item>
/// <item>While a live mission or an auto-resolve simulation owns the event (<see cref="BattleModeRegistry"/>),
/// the option is shown but refused, and the claim's release restores it — one player's surrender must not
/// conclude a battle other players are resolving (BR-001's exclusivity extended to surrender; the authoritative
/// server backstop is covered by <c>BattleSurrenderTests</c>).</item>
/// <item>A defender that can still fight gains nothing: the option keeps its native gating.</item>
/// </list>
/// </summary>
public class EncounterSurrenderOptionTests : MapEventTestBase
{
    public EncounterSurrenderOptionTests(ITestOutputHelper output) : base(output) { }

    /// <summary>
    /// The reported soft-lock: the defender's own party is spent (wounded hero, nobody healthy) while the
    /// client's view of the side still counts healthy troops in another party, so native's side-spent branch
    /// (<c>DefenderSide.TroopCount == own NumberOfHealthyMembers</c>) stays false and native alone would show
    /// no surrender. The coop condition must offer it enabled.
    /// </summary>
    [Fact]
    public void IncapacitatedDefender_WithHealthyTroopsElsewhereOnSide_GetsSurrenderOption()
    {
        var (ctx, client) = SetupIncapacitatedDefenderInBattle(withHealthyAllyOnSide: true);

        client.Call(() =>
        {
            // The state native hides surrender in: the side still counts healthy members the player's own
            // party does not have — the native side-spent branch is false, so a shown option is the coop fix.
            Assert.NotEqual(
                PartyBase.MainParty.NumberOfHealthyMembers,
                MobileParty.MainParty.MapEvent.DefenderSide.TroopCount);

            var (shown, args) = InvokeSurrenderCondition();

            Assert.True(shown, "an incapacitated defender must always be offered surrender");
            Assert.True(args.IsEnabled);
            Assert.Equal(GameMenuOption.LeaveType.Surrender, args.optionLeaveType);
        }, MapEventDisabledMethods);
    }

    /// <summary>
    /// While a live mission owns the event, the incapacitated defender's surrender is shown but refused
    /// (another player is fighting the battle); the claim's release (the state the mission-end broadcast
    /// leaves behind) restores the enabled option.
    /// </summary>
    [Fact]
    public void IncapacitatedDefender_WhileMissionOwnsEvent_SurrenderRefusedUntilRelease()
        => AssertSurrenderRefusedWhileClaimed(BattleStartMode.Mission);

    /// <summary>
    /// Same refusal while an auto-resolve simulation owns the event (another player is simulating the battle).
    /// </summary>
    [Fact]
    public void IncapacitatedDefender_WhileSimulationOwnsEvent_SurrenderRefusedUntilRelease()
        => AssertSurrenderRefusedWhileClaimed(BattleStartMode.Simulation);

    /// <summary>
    /// The claim refusal also applies when NATIVE offers the option (a defender whose whole side is spent):
    /// no surrender may conclude a claimed event, whoever enabled the option.
    /// </summary>
    [Fact]
    public void SoloSpentDefender_NativeSurrender_RefusedWhileEventClaimed()
    {
        var (ctx, client) = SetupIncapacitatedDefenderInBattle(withHealthyAllyOnSide: false);

        client.Call(() =>
        {
            // Native's own side-spent branch holds here (the party is the whole side): surrender is native-shown.
            Assert.Equal(
                PartyBase.MainParty.NumberOfHealthyMembers,
                MobileParty.MainParty.MapEvent.DefenderSide.TroopCount);

            BattleModeRegistry.Begin(ctx.MapEventId, BattleStartMode.Mission);
            try
            {
                var (shown, args) = InvokeSurrenderCondition();

                Assert.True(shown);
                Assert.False(args.IsEnabled, "surrender must be refused while a mission resolves the event");
            }
            finally
            {
                BattleModeRegistry.End();
            }
        }, MapEventDisabledMethods);
    }

    /// <summary>
    /// No over-opening: a defender whose main hero can still fight keeps the native gating (no surrender).
    /// </summary>
    [Fact]
    public void HealthyDefender_SurrenderKeepsNativeGating()
    {
        var (_, client) = SetupDefenderInBattle(withHealthyAllyOnSide: true);

        client.Call(() =>
        {
            var hero = Hero.MainHero;
            Assert.NotNull(hero);
            hero._health = 100;
            Assert.False(hero.IsWounded);

            var (shown, _) = InvokeSurrenderCondition();

            Assert.False(shown, "a defender that can still fight must not gain a surrender option");
        }, MapEventDisabledMethods);
    }

    /// <summary>
    /// No over-opening: a wounded defender whose own party still has healthy members can send its troops
    /// instead, so the option keeps the native gating (no surrender).
    /// </summary>
    [Fact]
    public void WoundedDefenderWithOwnHealthyTroops_SurrenderKeepsNativeGating()
    {
        var (ctx, client) = SetupDefenderInBattle(withHealthyAllyOnSide: true);

        var troopId = TestEnvironment.CreateRegisteredObject<CharacterObject>();
        SeedPartyTroopOnAll(ctx.DefenderPartyId, troopId, 3);

        client.Call(() =>
        {
            var hero = Hero.MainHero;
            Assert.NotNull(hero);
            hero._health = 1;
            Assert.True(hero.IsWounded);
            Assert.True(PartyBase.MainParty.NumberOfHealthyMembers > 0);

            var (shown, _) = InvokeSurrenderCondition();

            Assert.False(shown, "a wounded defender with healthy troops of its own must not gain a surrender option");
        }, MapEventDisabledMethods);
    }

    // ------------------------------------------------------------------
    // Drivers
    // ------------------------------------------------------------------

    private void AssertSurrenderRefusedWhileClaimed(BattleStartMode mode)
    {
        var (ctx, client) = SetupIncapacitatedDefenderInBattle(withHealthyAllyOnSide: true);

        client.Call(() =>
        {
            BattleModeRegistry.Begin(ctx.MapEventId, mode);
            try
            {
                var (shown, args) = InvokeSurrenderCondition();

                Assert.True(shown);
                Assert.False(args.IsEnabled, $"surrender must be refused while a {mode} resolves the event");
                Assert.NotNull(args.Tooltip);
            }
            finally
            {
                BattleModeRegistry.End();
            }

            // The released claim restores the option — the live path is NetworkBattleModeSet → Unclaimed.
            var (shownAfter, argsAfter) = InvokeSurrenderCondition();

            Assert.True(shownAfter);
            Assert.True(argsAfter.IsEnabled);
        }, MapEventDisabledMethods);
    }

    /// <summary>Runs the real (Harmony-patched) surrender menu condition in the calling instance's scope.</summary>
    private static (bool shown, MenuCallbackArgs args) InvokeSurrenderCondition()
    {
        var behavior = new EncounterGameMenuBehavior();
        var args = new MenuCallbackArgs((MenuContext)null, null);

        var method = AccessTools.Method(typeof(EncounterGameMenuBehavior), "game_menu_encounter_surrender_on_condition");
        Assert.NotNull(method);

        var shown = (bool)method.Invoke(behavior, new object[] { args });
        return (shown, args);
    }

    // ------------------------------------------------------------------
    // Setup
    // ------------------------------------------------------------------

    /// <summary>
    /// Battle with the first client's <see cref="MobileParty.MainParty"/> as the defender, at the encounter
    /// state the surrender condition reads. Optionally reinforces the defender side with a server-created
    /// party carrying healthy troops — the "side still counts healthy members" shape of the soft-lock.
    /// </summary>
    private (MapEventContext ctx, EnvironmentInstance client) SetupDefenderInBattle(bool withHealthyAllyOnSide)
    {
        var ctx = CreateServerMapEvent();
        var client = Clients.First();

        if (withHealthyAllyOnSide)
        {
            var allyPartyId = JoinNewServerPartyToSide(ctx.MapEventId, BattleSideEnum.Defender);
            var troopId = TestEnvironment.CreateRegisteredObject<CharacterObject>();
            SeedPartyTroopOnAll(allyPartyId, troopId, 3);
        }

        SetMainPartyInBattle(client, ctx.DefenderPartyId);
        EnsureClientMenuModels(client);

        return (ctx, client);
    }

    /// <summary>Defender battle state plus the incapacitation: wounded main hero, no healthy member left in
    /// the main party — the reported soft-lock state.</summary>
    private (MapEventContext ctx, EnvironmentInstance client) SetupIncapacitatedDefenderInBattle(bool withHealthyAllyOnSide)
    {
        var (ctx, client) = SetupDefenderInBattle(withHealthyAllyOnSide);
        MakeMainPartyIncapacitated(client);
        return (ctx, client);
    }

    /// <summary>Makes <paramref name="partyId"/> the client's <see cref="MobileParty.MainParty"/> and asserts
    /// it is currently in the battle. Runs in the client's static scope, where <c>Campaign.Current</c> resolves
    /// to that client.</summary>
    private void SetMainPartyInBattle(EnvironmentInstance client, string partyId)
    {
        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Campaign.Current.MainParty = party;
            Assert.Same(party, MobileParty.MainParty);
            Assert.NotNull(MobileParty.MainParty.Party.MapEventSide);
        }, MapEventDisabledMethods);
    }

    /// <summary>Wounds the client's main hero (the roster-independent hero the menu conditions read) and
    /// empties the main party's roster — the reported "injured and does not have troops" state, leaving
    /// <see cref="PartyBase.NumberOfHealthyMembers"/> at 0.</summary>
    private void MakeMainPartyIncapacitated(EnvironmentInstance client)
    {
        client.Call(() =>
        {
            using (new AllowedThread())
            {
                var hero = Hero.MainHero;
                Assert.NotNull(hero);
                // The publicized health field skips the HitPoints setter's wounded-transition dispatch,
                // which needs live campaign UI state the harness does not have.
                hero._health = 1;
                Assert.True(hero.IsWounded, "main hero should be wounded");

                // Remove by each element's actual counts (the EmptyRoster pattern) — wholesale wounding is
                // unreliable for hero elements, whose roster wounded count follows the hero's own state.
                var roster = PartyBase.MainParty.MemberRoster;
                for (int i = roster.Count - 1; i >= 0; i--)
                {
                    var element = roster.GetElementCopyAtIndex(i);
                    roster.AddToCountsAtIndex(i, -element.Number, -element.WoundedNumber, 0, false);
                }
            }

            Assert.Equal(0, PartyBase.MainParty.NumberOfHealthyMembers);
        }, MapEventDisabledMethods);
    }

    /// <summary>
    /// The harness Campaign boots without <c>Campaign.Models</c>. The surrender condition walks two of them:
    /// <c>CharacterStatsModel.WoundedHitPointLimit</c> (via <see cref="Hero.IsWounded"/>) and, for a wounded
    /// hero, <c>PartyMoraleModel</c> (native's broken-morale branch via <see cref="MobileParty.Morale"/>).
    /// The morale model is a fixed mid-scale stand-in: the live mid-battle client state the soft-lock
    /// reproduces (base 50, battle penalties not landed), high enough that the broken-morale branch stays
    /// false without walking the default model's food/wage/party-size dependencies.
    /// </summary>
    private void EnsureClientMenuModels(EnvironmentInstance client)
    {
        client.Call(() =>
        {
            if (Campaign.Current.Models != null) return;

            var models = new List<GameModel>
            {
                new DefaultCharacterStatsModel(),
                new FixedPartyMoraleModel(),
            };

            var gameModels = client.GameInstance.Game.AddGameModelsManager<GameModels>(models);
            AccessTools.Field(typeof(Campaign), "_gameModels").SetValue(Campaign.Current, gameModels);
        });
    }

    private sealed class FixedPartyMoraleModel : PartyMoraleModel
    {
        public override float HighMoraleValue => 70f;
        public override int GetDailyStarvationMoralePenalty(PartyBase party) => 0;
        public override int GetDailyNoWageMoralePenalty(MobileParty party) => 0;
        public override float GetStandardBaseMorale(PartyBase party) => 50f;
        public override float GetVictoryMoraleChange(PartyBase party) => 0f;
        public override float GetDefeatMoraleChange(PartyBase party) => 0f;
        public override ExplainedNumber GetEffectivePartyMorale(MobileParty party, bool includeDescription = false)
            => new ExplainedNumber(50f, includeDescription);
    }
}
