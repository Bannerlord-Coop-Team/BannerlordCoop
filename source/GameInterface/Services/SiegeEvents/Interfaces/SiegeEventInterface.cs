using Common;
using Common.Logging;
using Common.Util;
using GameInterface.Services.SiegeEvents.Handlers;
using GameInterface.Services.SiegeEvents.Patches;
using GameInterface.Services.SiegeEvents.Validation;
using Serilog;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using TaleWorlds.Library;
using static TaleWorlds.CampaignSystem.Siege.SiegeEvent;

namespace GameInterface.Services.SiegeEvents.Interfaces;

public readonly struct PendingSiegeAftermathPrompt
{
    public MobileParty LeaderParty { get; }
    public Settlement Settlement { get; }

    public PendingSiegeAftermathPrompt(MobileParty leaderParty, Settlement settlement)
    {
        LeaderParty = leaderParty;
        Settlement = settlement;
    }
}

/// <summary>
/// Applies siege entry and exit changes to the game. Callers are responsible for marshalling onto
/// the game thread (and, on the client, for the <c>AllowedThread</c> scope). The server must NOT use
/// an allowed thread so the siege object creation and camp writes replicate normally.
/// </summary>
public interface ISiegeEventInterface : IGameAbstraction
{
    /// <summary>
    /// Starts a siege of the settlement led by the given party. Server side.
    /// </summary>
    void StartSiegeEvent(MobileParty besiegerParty, Settlement settlement);

    /// <summary>
    /// Adds a party to the settlement's besieger camp. Server side.
    /// </summary>
    void JoinSiegeCamp(MobileParty party, Settlement settlement);

    /// <summary>
    /// Removes a party from its besieger camp; the siege ends when the last besieger leaves. Server side.
    /// </summary>
    void BreakSiege(MobileParty party);

    /// <summary>
    /// Removes only this party from its besieger camp without applying vanilla's attached-party cascade.
    /// Server side.
    /// </summary>
    /// <returns>Whether the stale-link repair path was required instead of the vanilla setter.</returns>
    bool BreakSiegeForPartyOnly(MobileParty party);

    /// <summary>
    /// Directly clears a besieger-camp link after the server identified a malformed siege graph.
    /// </summary>
    void ClearStaleBesiegerCamp(MobileParty party);

    /// <summary>
    /// Runs the player-local part of starting a siege: close the encounter and open the siege menus.
    /// </summary>
    void StartLocalPlayerSiegePreparation();

    /// <summary>
    /// Runs the player-local part of joining an ongoing siege camp.
    /// </summary>
    void StartLocalPlayerJoinedSiege(Settlement settlement);

    /// <summary>
    /// Runs the player-local part of leaving a siege camp.
    /// </summary>
    void FinishLocalPlayerSiegeLeave();

    /// <summary>
    /// Applies a player's parked siege aftermath choice. Server side.
    /// </summary>
    void ApplySiegeAftermathChoice(MobileParty party, Settlement settlement, int aftermathType);

    /// <summary>
    /// Returns a stable snapshot of valid server-owned aftermath prompts. Used to re-prompt a
    /// leader when that client enters the campaign after a reload or reconnect.
    /// </summary>
    PendingSiegeAftermathPrompt[] GetPendingSiegeAftermathPrompts();

    /// <summary>
    /// Opens the local defender's encounter prompt for a starting siege assault, when this player's
    /// party is inside the assaulted settlement.
    /// </summary>
    void PromptSiegeDefense(MobileParty attackerParty, Settlement settlement);

    /// <summary>
    /// Switches this player to the vanilla siege-preparation menu when its party is inside a
    /// settlement a siege just started against.
    /// </summary>
    void PromptSiegePreparation(MobileParty attackerParty, Settlement settlement);

    /// <summary>
    /// Frees this player from the siege-preparation menus when the siege dissolved without a battle.
    /// </summary>
    void PromptSiegeEnded(Settlement settlement, bool besiegerDefeated);

    /// <summary>
    /// Seat a winning inside defender on the siege-defeated menu after the assault, which the replicated
    /// event teardown otherwise bypasses (the winner falls through to the settlement arrival menu).
    /// </summary>
    void PromptSiegeDefenderVictory(Settlement settlement);

    /// <summary>
    /// Applies the canonical local menu and player-siege state returned by authoritative entry or
    /// reconnect validation.
    /// </summary>
    void ReconcileSiegeEntry(SiegeEntryDisposition disposition, Settlement settlement);

    /// <summary>
    /// Recomputes and applies the reloaded local party's final siege disposition after campaign
    /// catch-up has completed.
    /// </summary>
    void ReconcileReloadedSiegeEntry();

    /// <summary>
    /// Establishes the besieging player's encounter for a starting wall assault by adopting the replicated
    /// assault map event, so it can then enter the mission.
    /// </summary>
    void PromptSiegeAssault(MobileParty attackerParty, Settlement settlement);

    /// <summary>
    /// Records the aftermath the server applied so the local settlement-taken menus narrate it, and
    /// releases the capture-menu hold for the settlement (the choice is resolved).
    /// </summary>
    void SetLocalAftermathNarration(Settlement settlement, int aftermathType);

    /// <summary>
    /// Marks the settlement whose applied aftermath the local winning participant's menus need.
    /// This is separate from the leader-only choice/hold identity.
    /// </summary>
    void SetLocalAftermathNarrationContext(Settlement settlement);

    /// <summary>
    /// Opens the settlement-taken choice menu when this client leads the parked aftermath and its
    /// own encounter flow hasn't opened it already.
    /// </summary>
    void PromptLocalAftermathChoice(MobileParty leaderParty, Settlement settlement);

    /// <summary>
    /// Backstop that routes a stale post-capture siege-assault encounter (a host attacker whose gated aftermath
    /// transition missed) into the settlement-taken menu. Driven off the encounter menu, not the prompt.
    /// </summary>
    void RouteCapturedSettlementToAftermathMenu(Settlement settlement);

    /// <summary>
    /// Builds/deploys a siege engine at a slot for one side, mirroring the map production popup.
    /// Server side; rejected while the siege is fighting an assault, matching the vanilla tick freeze.
    /// </summary>
    void DeploySiegeEngine(SiegeEvent siegeEvent, BattleSideEnum side, SiegeEngineType engineType, int index);

    /// <summary>
    /// Removes a deployed siege engine from its slot for one side. Server side; same assault gate.
    /// </summary>
    void RemoveDeployedSiegeEngine(SiegeEvent siegeEvent, BattleSideEnum side, int index, bool isRanged, bool moveToReserve);
}

internal class SiegeEventInterface : ISiegeEventInterface, IDisposable
{
    private static readonly ILogger Logger = LogManager.GetLogger<SiegeEventInterface>();
    private readonly ISiegeEntryValidator siegeEntryValidator;
    private bool siegeEntryReconciliationPending;
    private bool siegeEntryReconciliationSubscribed;
    private Settlement localAftermathChoiceSettlement;
    private Settlement localAftermathNarrationSettlement;

    public SiegeEventInterface(ISiegeEntryValidator siegeEntryValidator)
    {
        if (siegeEntryValidator == null)
            throw new ArgumentNullException(nameof(siegeEntryValidator));

        this.siegeEntryValidator = siegeEntryValidator;
    }

    internal SiegeEventInterface() : this(new SiegeEntryValidator())
    {
    }

    public void StartSiegeEvent(MobileParty besiegerParty, Settlement settlement)
    {
        // Vanilla's besiege consequence calls PlayerEncounter.Finish() first, which leaves the settlement when the
        // besieger is inside it. In co-op that Finish runs only on the client (under AllowedThread), so its leave
        // never reaches the server - which then keeps the besieger marked inside, and vanilla's sally-out scan skips
        // parties with CurrentSettlement != null (reading our besieger as zero strength). Leave here to match vanilla.
        if (besiegerParty.CurrentSettlement != null)
        {
            LeaveSettlementAction.ApplyForParty(besiegerParty);
        }

        Campaign.Current.SiegeEventManager.StartSiegeEvent(settlement, besiegerParty);
    }

    public void JoinSiegeCamp(MobileParty party, Settlement settlement)
    {
        // Vanilla's join-siege consequence leaves the settlement before assigning the besieger camp.
        // The client repeats that transition under AllowedThread for its encounter/menu state, so the
        // authoritative copy has to leave here as well or it remains both inside and besieging.
        if (party.CurrentSettlement != null)
        {
            LeaveSettlementAction.ApplyForParty(party);
        }

        party.BesiegerCamp = settlement.SiegeEvent?.BesiegerCamp;
    }

    public void BreakSiege(MobileParty party)
    {
        party.BesiegerCamp = null;
    }

    public bool BreakSiegeForPartyOnly(MobileParty party)
    {
        var camp = party?.BesiegerCamp;
        if (camp == null) return false;

        if (!CanApplyVanillaSiegeLeave(party, camp))
        {
            ClearStaleBesiegerCamp(party);
            return true;
        }

        var attachedParties = party._attachedParties;
        party._attachedParties = new MBList<MobileParty>();
        try
        {
            party.BesiegerCamp = null;
        }
        finally
        {
            party._attachedParties = attachedParties;
        }

        return false;
    }

    public void ClearStaleBesiegerCamp(MobileParty party)
    {
        var camp = party?.BesiegerCamp;
        if (camp == null) return;

        if (party.IsMainParty && party.Anchor != null)
        {
            party.Anchor.IsDisabled = false;
        }
        party.EventPositionAdder = Vec2.Zero;
        camp._besiegerParties?.Remove(party);
        party._besiegerCamp = null;
        party._besiegerCampResetStarted = false;
        party.Party?.SetVisualAsDirty();
        Logger.Warning(
            "Cleared a structurally stale besieger-camp link for party {Party}",
            party.StringId);
    }

    private static bool CanApplyVanillaSiegeLeave(
        MobileParty party,
        BesiegerCamp camp)
    {
        var siegeEvent = camp.SiegeEvent;
        var settlement = siegeEvent?.BesiegedSettlement;
        return siegeEvent != null &&
            settlement != null &&
            siegeEvent.BesiegerCamp == camp &&
            settlement.SiegeEvent == siegeEvent &&
            camp._besiegerParties != null &&
            camp._besiegerParties.Contains(party) &&
            party._attachedParties != null;
    }

    public void StartLocalPlayerSiegePreparation()
    {
        if (PlayerEncounter.Current != null)
        {
            PlayerEncounter.Finish();
        }

        PlayerSiege.StartPlayerSiege(BattleSideEnum.Attacker);
        PlayerSiege.StartSiegePreparation();
    }

    public void StartLocalPlayerJoinedSiege(Settlement settlement)
    {
        if (Hero.MainHero.CurrentSettlement != null)
        {
            PlayerEncounter.LeaveSettlement();
        }

        if (PlayerEncounter.Current != null)
        {
            PlayerEncounter.Finish();
        }

        PlayerSiege.StartPlayerSiege(BattleSideEnum.Attacker, isSimulation: false, settlement);
        PlayerSiege.StartSiegePreparation();
    }

    public void FinishLocalPlayerSiegeLeave()
    {
        if (PlayerEncounter.Current != null)
        {
            PlayerEncounter.Finish();
        }
        else
        {
            GameMenu.ExitToLast();
        }
    }

    public void ApplySiegeAftermathChoice(MobileParty party, Settlement settlement, int aftermathType)
    {
        if (!Patches.SiegeAftermathPatches.PendingAftermaths.TryGetValue(settlement, out var pending))
        {
            Logger.Error("No pending siege aftermath for {Settlement}", settlement.Name?.ToString());
            return;
        }

        // Validate before removing so a mismatched request cannot destroy the pending entry.
        if (pending.LeaderParty != party)
        {
            Logger.Error("Party {Party} is not the pending aftermath leader for {Settlement}", party.StringId, settlement.Name?.ToString());
            return;
        }

        if (!pending.MatchesCurrentCapture(settlement))
        {
            Patches.SiegeAftermathPatches.PendingAftermaths.TryRemove(settlement, out _);
            Logger.Warning("Rejected stale siege aftermath choice for {Settlement}: the capture owner or capturer changed",
                settlement.Name?.ToString());
            return;
        }

        Patches.SiegeAftermathPatches.PendingAftermaths.TryRemove(settlement, out _);

        SiegeAftermathAction.ApplyAftermath(party, settlement, (SiegeAftermathAction.SiegeAftermath)aftermathType, pending.PreviousOwnerClan, pending.Contributions);
    }

    public PendingSiegeAftermathPrompt[] GetPendingSiegeAftermathPrompts()
    {
        return Patches.SiegeAftermathPatches.PendingAftermaths
            .Where(pair => pair.Value.MatchesCurrentCapture(pair.Key))
            .Select(pair => new PendingSiegeAftermathPrompt(pair.Value.LeaderParty, pair.Key))
            .ToArray();
    }

    public void ReconcileReloadedSiegeEntry()
    {
        if (!(GameStateManager.Current?.ActiveState is TaleWorlds.CampaignSystem.GameState.MapState))
        {
            siegeEntryReconciliationPending = true;
            if (!siegeEntryReconciliationSubscribed)
            {
                siegeEntryReconciliationSubscribed = true;
                CampaignEvents.TickEvent.AddNonSerializedListener(this, RetrySiegeEntryReconciliation);
            }
            return;
        }

        ClearSiegeEntryReconciliationRetry();
        var validation = siegeEntryValidator.ValidateReloadedBesieger(MobileParty.MainParty);
        if (!validation.IsValid && MobileParty.MainParty?.BesiegerCamp != null)
        {
            using (new AllowedThread())
            {
                BreakSiegeForPartyOnly(MobileParty.MainParty);
            }
        }

        var canonicalState = validation.IsValid
            ? validation.CanonicalState
            : new SiegeEntryCanonicalState(SiegeEntryDisposition.Map, null);
        ReconcileSiegeEntry(canonicalState.Disposition, canonicalState.Settlement);
    }

    public void ReconcileSiegeEntry(SiegeEntryDisposition disposition, Settlement settlement)
    {
        if (disposition == SiegeEntryDisposition.Besieger)
        {
            if (MobileParty.MainParty?.BesiegerCamp?.SiegeEvent?.BesiegedSettlement != settlement)
                return;

            if (PlayerSiege.PlayerSiegeEvent != null &&
                PlayerSiege.BesiegedSettlement != settlement)
            {
                using (new AllowedThread())
                {
                    PlayerSiege.FinalizePlayerSiege();
                }
            }

            RestoreReloadedPlayerBesieging();
            return;
        }

        using (new AllowedThread())
        {
            if (PlayerSiege.PlayerSiegeEvent != null)
                PlayerSiege.FinalizePlayerSiege();
        }

        if (disposition == SiegeEntryDisposition.Settlement)
        {
            if (MobileParty.MainParty?.CurrentSettlement == settlement)
                RestoreReloadedPlayerInSettlement();
            return;
        }

        if (disposition == SiegeEntryDisposition.MapEvent)
            return;

        using (new AllowedThread())
        {
            if (PlayerEncounter.Current != null && MobileParty.MainParty?.MapEvent == null)
            {
                PlayerEncounter.Finish();
                return;
            }

            var currentMenu = Campaign.Current?.CurrentMenuContext?.GameMenu?.StringId;
            if (currentMenu == "town_outside" ||
                currentMenu == "castle_outside" ||
                currentMenu == "join_siege_event" ||
                currentMenu == "menu_siege_strategies" ||
                currentMenu == "encounter_interrupted_siege_preparations")
            {
                GameMenu.ExitToLast();
            }
        }
    }

    private void RestoreReloadedPlayerBesieging()
    {
        var party = MobileParty.MainParty;
        var settlement = party.BesiegerCamp.SiegeEvent?.BesiegedSettlement;
        if (settlement?.Party == null) return;

        // The headless server's save carries no player-siege state for this hero: vanilla only
        // reopens the siege menu from its own save's menu id (MapStateData.GameMenuId) and derives
        // PlayerSiege from MainParty, so after the character switch nothing re-drives the map-side
        // siege activation or the menu. Re-run the live entry (see StartLocalPlayerSiegePreparation).
        using (new AllowedThread())
        {
            PlayerSiege.StartPlayerSiege(BattleSideEnum.Attacker, isSimulation: false, settlement);
        }

        if (party.MapEvent != null)
        {
            if (party.MapEvent.MapEventSettlement != settlement)
            {
                Logger.Warning("Skipped the reloaded besieger's encounter at {Settlement}: the map event belongs to another settlement", settlement.StringId);
            }
            else
            {
                RestoreReloadedPlayerSiegeEncounter();
            }
            return;
        }

        // The menu vanilla's generic-state model picks for a besieging main party.
        using (new AllowedThread())
        {
            if (Campaign.Current.CurrentMenuContext == null)
            {
                GameMenu.ActivateGameMenu("menu_siege_strategies");
            }
            else
            {
                GameMenu.SwitchToMenu("menu_siege_strategies");
            }
        }
    }

    private static void RestoreReloadedPlayerSiegeEncounter()
    {
        // Vanilla serializes PlayerEncounter alongside MainParty.MapEvent. The client switches into
        // the server's hero without that player-local object, so rebuild it through InitAux, which
        // adopts MainParty.MapEvent for every siege battle type without creating a second event.
        if (PlayerEncounter.Current?._mapEvent == MobileParty.MainParty.MapEvent)
            return;

        using (new AllowedThread())
        {
            if (PlayerEncounter.Current != null)
            {
                PlayerEncounter.Finish(forcePlayerOutFromSettlement: false);
            }

            PlayerEncounter.Start();
            PlayerEncounter.Init();
        }
    }

    private void RestoreReloadedPlayerInSettlement()
    {
        var settlement = MobileParty.MainParty?.CurrentSettlement;
        if (settlement?.Party == null) return;

        // The headless server's save carries no player encounter for this hero; without one the
        // per-tick generic state menu resolves to the wrong screens (the besieger menu during a
        // siege, the gates menu otherwise).
        if (PlayerEncounter.Current == null)
        {
            using (new AllowedThread())
            {
                PlayerEncounter.Start();
                PlayerEncounter.Current.Init(PartyBase.MainParty, settlement.Party, settlement);
            }
        }

        var siegeEvent = settlement.SiegeEvent;
        if (siegeEvent == null) return;

        // An assault is already live: adopt the defender encounter like the live prompt does.
        if (settlement.Party.MapEvent != null)
        {
            var assaultAttacker = siegeEvent.BesiegerCamp?.LeaderParty;
            if (assaultAttacker != null) PromptSiegeDefense(assaultAttacker, settlement);
            return;
        }

        if (siegeEvent.BesiegerCamp?.LeaderParty == null) return;

        // A campaign tick may already have parked us on the besieger menu (there was no waiting
        // encounter at that point); switch regardless — this is the vanilla inside-defender
        // preparation menu.
        using (new AllowedThread())
        {
            if (Campaign.Current.CurrentMenuContext == null)
            {
                GameMenu.ActivateGameMenu("encounter_interrupted_siege_preparations");
            }
            else
            {
                GameMenu.SwitchToMenu("encounter_interrupted_siege_preparations");
            }
        }
    }

    public void PromptSiegePreparation(MobileParty attackerParty, Settlement settlement)
    {
        // Vanilla switches an inside player via the wait-menu interrupt tick, which only runs while
        // campaign time flows; a co-op client parked at the static town menu never re-evaluates its
        // menu, so the replicated prompt drives the same switch.
        if (MobileParty.MainParty?.CurrentSettlement != settlement) return;
        if (settlement.SiegeEvent?.BesiegerCamp?.LeaderParty == null) return;

        // A location scene (tavern etc.) owns the screen; vanilla never delivers this interrupt
        // there because time freezes in scenes, so skip rather than fight the scene.
        if (TaleWorlds.MountAndBlade.MissionState.Current != null)
        {
            Logger.Information("Skipped the siege preparation prompt at {Settlement}: a mission is running", settlement.StringId);
            return;
        }

        // Vanilla's own wait-menu interrupt may already have switched us.
        var currentMenu = Campaign.Current.CurrentMenuContext?.GameMenu?.StringId;
        if (currentMenu == "encounter_interrupted_siege_preparations" || currentMenu == "menu_siege_strategies") return;

        using (new AllowedThread())
        {
            if (currentMenu == null)
            {
                GameMenu.ActivateGameMenu("encounter_interrupted_siege_preparations");
            }
            else
            {
                GameMenu.SwitchToMenu("encounter_interrupted_siege_preparations");
            }
        }
    }

    public void PromptSiegeEnded(Settlement settlement, bool besiegerDefeated)
    {
        // Frees an inside player parked on the siege-preparation menus, whose leave option derefs
        // the now-torn-down SiegeEvent; the vanilla end menus have no init logic, so they are safe
        // after the replicated teardown.
        if (MobileParty.MainParty?.CurrentSettlement != settlement) return;

        var currentMenu = Campaign.Current.CurrentMenuContext?.GameMenu?.StringId;
        if (currentMenu != "encounter_interrupted_siege_preparations" && currentMenu != "menu_siege_strategies") return;

        using (new AllowedThread())
        {
            // The player joined the defense locally (PlayerSiege.StartPlayerSiege); clear its siege
            // map state so the visuals and camera release with the menu.
            if (PlayerSiege.PlayerSiegeEvent != null && PlayerSiege.BesiegedSettlement == settlement)
            {
                PlayerSiege.FinalizePlayerSiege();
            }

            GameMenu.SwitchToMenu(besiegerDefeated ? "siege_attacker_defeated" : "siege_attacker_left");
        }
    }

    public void PromptSiegeDefenderVictory(Settlement settlement)
    {
        if (settlement == null || MobileParty.MainParty == null) return;

        using (new AllowedThread())
        {
            // The player joined the defense locally (PlayerSiege); clear its siege map state so the camera
            // and visuals release with the menu.
            if (PlayerSiege.PlayerSiegeEvent != null && PlayerSiege.BesiegedSettlement == settlement)
            {
                PlayerSiege.FinalizePlayerSiege();
            }

            // Finish the stale pre-assault siege encounter whose map event the server already destroyed.
            if (PlayerEncounter.Current != null)
            {
                if (MobileParty.MainParty.Party._mapEventSide != null)
                    MobileParty.MainParty.Party._mapEventSide = null;
                PlayerEncounter.Finish(forcePlayerOutFromSettlement: false);
            }

            // The server holds this defender inside the settlement; reconcile the local copy (which the assault
            // may have left outside) so siege_attacker_defeated's "Return to {SETTLEMENT}" resolves. AllowedThread
            // keeps the enter local, not round-tripped.
            if (MobileParty.MainParty.CurrentSettlement != settlement)
            {
                EnterSettlementAction.ApplyForParty(MobileParty.MainParty, settlement);
            }
            EncounterManager.StartSettlementEncounter(MobileParty.MainParty, settlement);

            if (Campaign.Current?.CurrentMenuContext != null)
            {
                GameMenu.SwitchToMenu("siege_attacker_defeated");
            }
            else
            {
                GameMenu.ActivateGameMenu("siege_attacker_defeated");
            }
        }
    }

    public void PromptSiegeDefense(MobileParty attackerParty, Settlement settlement)
    {
        // Mirrors the defender branch of vanilla EncounterManager.StartSettlementEncounter, which never
        // runs on this machine because the attacker party is not controlled here.
        if (MobileParty.MainParty?.CurrentSettlement != settlement) return;

        var mapEvent = attackerParty.MapEvent;
        if (mapEvent == null) return;

        if (PartyBase.MainParty.MapEventSide != mapEvent.DefenderSide)
        {
            if (!mapEvent.CanPartyJoinBattle(PartyBase.MainParty, settlement.BattleSide))
            {
                // Vanilla kicks a non-joinable defender out of the settlement. Runs outside AllowedThread so
                // the leave routes through the normal co-op settlement-exit flow and replicates.
                LeaveSettlementAction.ApplyForParty(MobileParty.MainParty);
            }
            else
            {
                Logger.Warning("Skipped the siege defense prompt at {Settlement}: the authoritative defender assignment is missing", settlement.StringId);
            }
            return;
        }

        using (new AllowedThread())
        {
            if (PlayerEncounter.Current != null)
            {
                PlayerEncounter.Finish(forcePlayerOutFromSettlement: false);
            }

            PlayerEncounter.Start();
            PlayerEncounter.Current.Init(attackerParty.Party, settlement.Party, settlement);
        }
    }

    public void PromptSiegeAssault(MobileParty attackerParty, Settlement settlement)
    {
        // The besieging player adopts the already-replicated assault map event as its player encounter. Mirrors
        // the attacker branch of vanilla StartSettlementEncounter, which never runs here because the server
        // created and replicated the event. Only the parameterless PlayerEncounter.Init() adopts
        // MainParty.MapEvent (via InitAux); the 3-arg overload the defender uses re-creates the siege event for
        // an attacker (attacker == MainParty), which would desync it.
        if (MobileParty.MainParty?.BesiegedSettlement != settlement) return;

        var mapEvent = settlement.Party?.MapEvent;
        if (mapEvent == null || !mapEvent.IsSiegeAssault) return;
        if (MobileParty.MainParty.MapEvent == null) return;

        using (new AllowedThread())
        {
            if (PlayerEncounter.Current != null)
            {
                PlayerEncounter.Finish(forcePlayerOutFromSettlement: false);
            }

            PlayerEncounter.Start();
            PlayerEncounter.Init();
        }
    }

    public void PromptLocalAftermathChoice(MobileParty leaderParty, Settlement settlement)
    {
        // Identity-stable check: the network-resolved leaderParty can be a divergent instance after a host's
        // post-mission party resync, so a reference compare would silently drop the real local leader.
        if (leaderParty?.LeaderHero != Hero.MainHero) return;

        // This client is both the choice owner and a local narration participant. Keep the identities
        // separate: non-leader participants need narration too, but only the leader owns the menu hold.
        localAftermathChoiceSettlement = settlement;
        localAftermathNarrationSettlement = settlement;

        // Hold the aftermath menu open until the player picks (see SiegeCaptureMenuHoldPatch): a co-op
        // client can't pause, so its encounter would otherwise roll the choice menu out to the town menu.
        SiegeCaptureMenuHoldPatch.HoldFor(settlement);

        // Real-time assault capture: the prompt arrives while the battle mission is still tearing down, which is
        // too early to touch PlayerEncounter. Park the transition and let SiegeCaptureTransitionRetryHandler
        // re-run it on the next CampaignTick once the mission has fully popped back to the map.
        if (TaleWorlds.MountAndBlade.MissionState.Current != null)
        {
            SiegeCaptureTransitionRetryHandler.Arm(leaderParty, settlement);
            return;
        }

        var currentMenu = Campaign.Current?.CurrentMenuContext?.GameMenu?.StringId;
        if (currentMenu != null && (currentMenu.StartsWith("menu_settlement_taken") || currentMenu == "siege_aftermath_contextual_summary")) return;

        SwitchLocalPartyToSettlementTaken(settlement);
    }

    // Client-local capture-aftermath transition: finish the stale pre-mission siege encounter and open the
    // settlement-taken menu. Shared by the aftermath-choice prompt and the encounter-menu backstop that catches
    // a host attacker whose gated retry missed. The local player is the capturing leader (its clan now owns the
    // settlement), so besiegerParty is MainParty.
    private void SwitchLocalPartyToSettlementTaken(Settlement settlement)
    {
        // menu_settlement_taken_on_init routes on _besiegerParty == MainParty to reach the leader submenu that
        // carries Devastate/Pillage/Mercy; set the fields the client's OnMapEventEnded prefix would have set.
        var aftermathBehavior = Campaign.Current?.GetCampaignBehavior<SiegeAftermathCampaignBehavior>();
        if (aftermathBehavior != null)
        {
            aftermathBehavior._besiegerParty = MobileParty.MainParty;
            aftermathBehavior._prevSettlementOwnerClan = settlement.OwnerClan;
            aftermathBehavior._wasPlayerArmyMember = false;
        }

        using (new AllowedThread())
        {
            // Auto-resolve drops PlayerEncounter before the event teardown arrives; detach so settlement entry can run.
            if (MobileParty.MainParty.Party._mapEventSide != null)
                MobileParty.MainParty.Party._mapEventSide = null;

            // Finish the stale pre-mission siege encounter whose map event the server already ended.
            if (PlayerEncounter.Current != null)
            {
                PlayerEncounter.Finish(forcePlayerOutFromSettlement: false);
            }

            // AllowedThread stands the co-op EncounterManager patch down so this runs locally, not round-tripped.
            if (MobileParty.MainParty.CurrentSettlement != settlement)
            {
                EnterSettlementAction.ApplyForParty(MobileParty.MainParty, settlement);
            }
            EncounterManager.StartSettlementEncounter(MobileParty.MainParty, settlement);

            if (Campaign.Current?.CurrentMenuContext != null)
            {
                GameMenu.SwitchToMenu("menu_settlement_taken");
            }
            else
            {
                GameMenu.ActivateGameMenu("menu_settlement_taken");
            }
        }
    }

    public void RouteCapturedSettlementToAftermathMenu(Settlement settlement)
    {
        if (settlement == null) return;

        // Backstop for a HOST attacker: the aftermath-prompt transition parks in the gated retry (the host is
        // still in its own mission when the prompt arrives) and can miss. This runs off the observable stuck
        // encounter menu instead, so it lands regardless of why the prompt path failed.
        localAftermathChoiceSettlement = settlement;
        SiegeCaptureMenuHoldPatch.HoldFor(settlement);
        SwitchLocalPartyToSettlementTaken(settlement);
    }

    public void SetLocalAftermathNarration(Settlement settlement, int aftermathType)
    {
        if (localAftermathChoiceSettlement == settlement)
        {
            localAftermathChoiceSettlement = null;
        }

        var matchesLocalNarration = localAftermathNarrationSettlement == settlement;
        if (matchesLocalNarration)
        {
            localAftermathNarrationSettlement = null;
        }

        // The choice is resolved (ours, or the server auto-applied a stale one); without this release a
        // client whose pick never happened keeps bouncing its town menu back to menu_settlement_taken.
        SiegeCaptureMenuHoldPatch.Release(settlement);

        // Ignore another settlement's broadcast. The participant-bound identity remains valid throughout
        // the settlement-taken flow, including the short transition where Settlement.CurrentSettlement is null.
        if (!matchesLocalNarration) return;

        var behavior = Campaign.Current?.GetCampaignBehavior<SiegeAftermathCampaignBehavior>();
        if (behavior == null) return;

        behavior._playerEncounterAftermath = (SiegeAftermathAction.SiegeAftermath)aftermathType;

        // The settlement-taken menus read the field once in on_init and never re-tick, so if that menu
        // is already open when the server's pick lands, re-enter it to re-render the narration.
        var currentMenu = Campaign.Current?.CurrentMenuContext?.GameMenu?.StringId;
        if (currentMenu != null && currentMenu.StartsWith("menu_settlement_taken"))
        {
            using (new AllowedThread())
            {
                GameMenu.SwitchToMenu("menu_settlement_taken");
            }
        }
    }

    public void SetLocalAftermathNarrationContext(Settlement settlement)
    {
        localAftermathNarrationSettlement = settlement;
    }

    internal bool HasLocalAftermathNarrationContext(Settlement settlement)
    {
        return localAftermathNarrationSettlement == settlement;
    }

    private void RetrySiegeEntryReconciliation(float dt)
    {
        if (!siegeEntryReconciliationPending) return;
        if (!(GameStateManager.Current?.ActiveState is TaleWorlds.CampaignSystem.GameState.MapState)) return;

        ReconcileReloadedSiegeEntry();
    }

    private void ClearSiegeEntryReconciliationRetry()
    {
        siegeEntryReconciliationPending = false;
        if (!siegeEntryReconciliationSubscribed) return;

        siegeEntryReconciliationSubscribed = false;
        CampaignEvents.TickEvent.ClearListeners(this);
    }

    public void Dispose()
    {
        ClearSiegeEntryReconciliationRetry();
        SiegeCaptureMenuHoldPatch.Release(localAftermathChoiceSettlement);
        localAftermathChoiceSettlement = null;
        localAftermathNarrationSettlement = null;
    }

    public void DeploySiegeEngine(SiegeEvent siegeEvent, BattleSideEnum side, SiegeEngineType engineType, int index)
    {
        if (IsSiegeFightingAssault(siegeEvent))
        {
            Logger.Error("Rejecting siege engine deploy during an active assault of {Settlement}", siegeEvent.BesiegedSettlement?.Name?.ToString());
            return;
        }

        // Mirrors MapSiegeProductionVM.OnPossibleMachineSelection: reuse a matching reserved engine,
        // else start a new construction, and hand the side to the player-driven Custom strategy.
        var siegeEventSide = siegeEvent.GetSiegeEventSide(side);
        var progress = siegeEventSide.SiegeEngines.ReservedSiegeEngines.FirstOrDefault(engine => engine.SiegeEngine == engineType);
        if (progress == null)
        {
            float hitPoints = Campaign.Current.Models.SiegeEventModel.GetSiegeEngineHitPoints(siegeEvent, engineType, side);
            progress = new SiegeEngineConstructionProgress(engineType, 0f, hitPoints);
        }

        if (siegeEventSide.SiegeStrategy != DefaultSiegeStrategies.Custom)
        {
            siegeEventSide.SetSiegeStrategy(DefaultSiegeStrategies.Custom);
        }

        siegeEventSide.SiegeEngines.DeploySiegeEngineAtIndex(progress, index);
        siegeEvent.BesiegedSettlement.Party.SetVisualAsDirty();
    }

    public void RemoveDeployedSiegeEngine(SiegeEvent siegeEvent, BattleSideEnum side, int index, bool isRanged, bool moveToReserve)
    {
        if (IsSiegeFightingAssault(siegeEvent))
        {
            Logger.Error("Rejecting siege engine removal during an active assault of {Settlement}", siegeEvent.BesiegedSettlement?.Name?.ToString());
            return;
        }

        siegeEvent.GetSiegeEventSide(side).SiegeEngines.RemoveDeployedSiegeEngine(index, isRanged, moveToReserve);
        siegeEvent.BesiegedSettlement.Party.SetVisualAsDirty();
    }

    // Vanilla freezes the whole siege container while either leader party fights (SiegeEvent.Tick's
    // MapEvent gate); a request mutating it mid-assault would reorder the deployed list under the
    // host's positional end-of-mission engine report.
    private static bool IsSiegeFightingAssault(SiegeEvent siegeEvent)
    {
        return siegeEvent.BesiegerCamp?.LeaderParty?.MapEvent != null
            || siegeEvent.BesiegedSettlement?.Party?.MapEvent != null;
    }
}
