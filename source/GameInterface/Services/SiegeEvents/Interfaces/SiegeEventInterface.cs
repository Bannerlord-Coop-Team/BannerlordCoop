using Common;
using Common.Logging;
using Common.Util;
using GameInterface.Services.SiegeEvents.Handlers;
using GameInterface.Services.SiegeEvents.Patches;
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
    /// [Game thread] Rebuilds the local in-settlement state for a player whose reloaded party is
    /// inside a settlement: the encounter the headless server's save doesn't carry, and the siege
    /// menus when the settlement is besieged.
    /// </summary>
    void RestoreReloadedPlayerInSettlement();

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
    private bool reloadSettlementRestorePending;
    private bool reloadSettlementRestoreSubscribed;
    private Settlement localAftermathChoiceSettlement;
    private Settlement localAftermathNarrationSettlement;

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

    public void RestoreReloadedPlayerInSettlement()
    {
        // The queued restore can land before the map state is active. GameThread.RunSafe executes inline
        // when called from the game thread, so using it as a retry recurses immediately. Arm one campaign-
        // tick listener instead; campaign ticks resume only after the map state becomes active.
        if (!(GameStateManager.Current?.ActiveState is TaleWorlds.CampaignSystem.GameState.MapState))
        {
            reloadSettlementRestorePending = true;
            if (!reloadSettlementRestoreSubscribed)
            {
                reloadSettlementRestoreSubscribed = true;
                CampaignEvents.TickEvent.AddNonSerializedListener(this, RetryReloadedPlayerSettlementRestore);
            }
            return;
        }

        ClearReloadedPlayerSettlementRetry();

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

        if (!mapEvent.CanPartyJoinBattle(PartyBase.MainParty, settlement.BattleSide))
        {
            // Vanilla kicks a non-joinable defender out of the settlement. Runs outside AllowedThread so
            // the leave routes through the normal co-op settlement-exit flow and replicates.
            LeaveSettlementAction.ApplyForParty(MobileParty.MainParty);
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

    private void RetryReloadedPlayerSettlementRestore(float dt)
    {
        if (!reloadSettlementRestorePending) return;
        if (!(GameStateManager.Current?.ActiveState is TaleWorlds.CampaignSystem.GameState.MapState)) return;

        ClearReloadedPlayerSettlementRetry();
        RestoreReloadedPlayerInSettlement();
    }

    private void ClearReloadedPlayerSettlementRetry()
    {
        reloadSettlementRestorePending = false;
        if (!reloadSettlementRestoreSubscribed) return;

        reloadSettlementRestoreSubscribed = false;
        CampaignEvents.TickEvent.ClearListeners(this);
    }

    public void Dispose()
    {
        ClearReloadedPlayerSettlementRetry();
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
