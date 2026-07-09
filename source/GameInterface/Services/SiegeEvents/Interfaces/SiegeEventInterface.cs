using Common.Logging;
using Common.Util;
using GameInterface.Services.SiegeEvents.Handlers;
using GameInterface.Services.SiegeEvents.Patches;
using Serilog;
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
    /// Opens the local defender's encounter prompt for a starting siege assault, when this player's
    /// party is inside the assaulted settlement.
    /// </summary>
    void PromptSiegeDefense(MobileParty attackerParty, Settlement settlement);

    /// <summary>
    /// Establishes the besieging player's encounter for a starting wall assault by adopting the replicated
    /// assault map event, so it can then enter the mission.
    /// </summary>
    void PromptSiegeAssault(MobileParty attackerParty, Settlement settlement);

    /// <summary>
    /// Records the aftermath the server applied so the local settlement-taken menus narrate it.
    /// </summary>
    void SetLocalAftermathNarration(int aftermathType);

    /// <summary>
    /// Opens the settlement-taken choice menu when this client leads the parked aftermath and its
    /// own encounter flow hasn't opened it already.
    /// </summary>
    void PromptLocalAftermathChoice(MobileParty leaderParty, Settlement settlement);

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

internal class SiegeEventInterface : ISiegeEventInterface
{
    private static readonly ILogger Logger = LogManager.GetLogger<SiegeEventInterface>();

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

        Patches.SiegeAftermathPatches.PendingAftermaths.TryRemove(settlement, out _);

        SiegeAftermathAction.ApplyAftermath(party, settlement, (SiegeAftermathAction.SiegeAftermath)aftermathType, pending.PreviousOwnerClan, pending.Contributions);
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
        if (leaderParty != MobileParty.MainParty) return;

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

        // menu_settlement_taken_on_init routes on _besiegerParty == MainParty to reach the leader submenu
        // that carries Devastate/Pillage/Mercy. The client's OnMapEventEnded prefix sets those fields, but
        // it doesn't run for a battle the server auto-resolved, so set them here or the router falls
        // through to the Continue-only participant submenu. The apply itself stays server-owned.
        var aftermathBehavior = Campaign.Current?.GetCampaignBehavior<SiegeAftermathCampaignBehavior>();
        if (aftermathBehavior != null)
        {
            aftermathBehavior._besiegerParty = leaderParty;
            aftermathBehavior._prevSettlementOwnerClan = settlement.OwnerClan;
            aftermathBehavior._wasPlayerArmyMember = false;
        }

        using (new AllowedThread())
        {
            // A live PlayerEncounter here is never the settlement-taken one (that menu returned above). On the
            // real-time path it is the STALE pre-mission siege encounter the mission popped back to, whose map
            // event the server already destroyed, so its dead "encounter" menu NREs on the null MapEvent. Finish
            // it first. The auto-resolve path reaches here with no encounter, so only the rebuild below runs.
            if (PlayerEncounter.Current != null)
            {
                if (MobileParty.MainParty.Party._mapEventSide != null)
                    MobileParty.MainParty.Party._mapEventSide = null;
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

    public void SetLocalAftermathNarration(int aftermathType)
    {
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
