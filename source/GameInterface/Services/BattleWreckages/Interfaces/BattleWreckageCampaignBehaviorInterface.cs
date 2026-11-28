using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.BattleWreckages.Data;
using GameInterface.Services.BattleWreckages.Messages;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Players;
using GameInterface.Services.TroopRosters.Data;
using GameInterface.Services.TroopRosters.Interfaces;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.BattleWreckages;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.LogEntries;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace GameInterface.Services.BattleWreckages.Interfaces;

public interface IBattleWreckageCampaignBehaviorInterface : IGameAbstraction
{
    void AddWreckageToMap(BattleWreckage battleWreckage);
    void RemoveWreckageFromMap(BattleWreckage battleWreckage);
    void StartWreckageEncounter(BattleWreckage battleWreckage, Hero mainHero);
    void OpenWreckageEncounter(BattleWreckage battleWreckage, bool isInterrupted, CampaignTime requiredInvestigationDuration);
    void ShowWreckageEncounterDenied();
    void OnHourlyTick();
    void OnPartyEncounter(PartyBase attackerParty, PartyBase defenderParty);
    void OnHeroPrisonerTakenEvent(Hero hero);
    void LeaveWreckageEncounter(Hero mainHero, BattleWreckageCampaignBehavior.WreckageLeaveReason reason);
    void CloseWreckageEncounter(
        BattleWreckageCampaignBehavior.WreckageLeaveReason reason,
        CampaignTime requiredInvestigationDuration);
    void ShowWreckageInvestigationResults(
        TroopRosterData lootedTroops,
        IEnumerable<ItemRosterElement> lootedItems,
        IEnumerable<TextObject> consequenceExplanations,
        int lootedGoldAmount,
        Settlement foundHideout);
    void SelectWreckageConsequences(Hero mainHero);
    void ShowRecoverTroopsDecision(int recoverTroopCount, CampaignTime requiredInvestigationDuration);
    void AcceptRecoverTroops(Hero mainHero);
    void InterruptDisconnectedPlayers();
}

public class BattleWreckageCampaignBehaviorInterface : IBattleWreckageCampaignBehaviorInterface
{
    private static readonly ILogger Logger = LogManager.GetLogger<BattleWreckageCampaignBehaviorInterface>();

    private readonly IBattleWreckageEncounterTracker encounterTracker;
    private readonly IPlayerManager playerManager;
    private readonly IMessageBroker messageBroker;
    private readonly ITroopRosterInterface troopRosterInterface;

    public BattleWreckageCampaignBehaviorInterface(
        IBattleWreckageEncounterTracker encounterTracker,
        IPlayerManager playerManager,
        IMessageBroker messageBroker,
        ITroopRosterInterface troopRosterInterface)
    {
        this.encounterTracker = encounterTracker;
        this.playerManager = playerManager;
        this.messageBroker = messageBroker;
        this.troopRosterInterface = troopRosterInterface;
    }

    public void AddWreckageToMap(BattleWreckage battleWreckage)
    {
        // The end of vanilla BattleWreckage.CreateWreckage, which never runs on a client.
        using (new AllowedThread())
        {
            Campaign.Current.CampaignObjectManager.AddWreckage(battleWreckage);
            CampaignEventDispatcher.Instance.OnMapInteractableCreated(battleWreckage);
            LogEntry.AddLogEntry(new WreckageCreatedLogEntry(battleWreckage));
        }
    }

    public void RemoveWreckageFromMap(BattleWreckage battleWreckage)
    {
        // Vanilla BattleWreckage.DestroyWreckage, whose prefix skips it on clients.
        using (new AllowedThread())
        {
            Campaign.Current.CampaignObjectManager.RemoveWreckage(battleWreckage);
            CampaignEventDispatcher.Instance.OnMapInteractableDestroyed(battleWreckage);
        }
    }

    public void StartWreckageEncounter(BattleWreckage battleWreckage, Hero mainHero)
    {
        if (!TryTakeWreckage(battleWreckage, mainHero)) return;

        // Server half of vanilla SetCurrentEncounteredBattleWreckage, returning to the interrupted wreckage resumes its remaining time
        var isInterrupted = encounterTracker.TryGetEncounter(mainHero, out var encounter) && encounter.BattleWreckage == battleWreckage;
        if (!isInterrupted)
        {
            if (!TryGetBattleWreckageBehavior(out var behavior)) return;

            encounter = new BattleWreckageEncounter(mainHero, battleWreckage)
            {
                RequiredInvestigationDuration = GetInvestigationDuration(behavior, battleWreckage, mainHero),
            };

            // Replaces any encounter at another wreckage, like vanilla switching _encounteredBattleWreckage
            encounterTracker.AddEncounter(encounter);
        }

        encounter.IsInvestigating = true;
        encounter.InvestigationStartTime = CampaignTime.Now;

        var message = new WreckageEncounterStarted(battleWreckage, mainHero, isInterrupted, encounter.RequiredInvestigationDuration);
        messageBroker.Publish(this, message);
    }

    public void OpenWreckageEncounter(BattleWreckage battleWreckage, bool isInterrupted, CampaignTime requiredInvestigationDuration)
    {
        if (!TryGetBattleWreckageBehavior(out var behavior)) return;

        // Vanilla works out a resume from the kept wreckage, so the server's answer is loaded into it first,
        // along with the remaining time the resume menu shows
        behavior._encounteredBattleWreckage = isInterrupted ? battleWreckage : null;
        behavior._requiredInvestigationDuration = requiredInvestigationDuration;

        // Vanilla holds the party and opens the wreckage, remains or blocking menu
        behavior.SetCurrentEncounteredBattleWreckage(battleWreckage);

        // A new investigation rolls its own duration in vanilla, and the server's is the one its interrupt math uses
        behavior._requiredInvestigationDuration = requiredInvestigationDuration;
    }

    public void ShowWreckageEncounterDenied()
    {
        InformationManager.DisplayMessage(new InformationMessage(GameTexts.FindText("str_coop_battle_wreckage_in_use").ToString()));
    }

    public void OnHourlyTick()
    {
        // The server has no encountered wreckage or visibility, so tracked encounters are what keep a wreckage alive
        var encounteredWreckages = encounterTracker.Encounters.Select(encounter => encounter.BattleWreckage).ToList();

        for (int i = Campaign.Current.Wreckages.Count - 1; i >= 0; i--)
        {
            var battleWreckage = Campaign.Current.Wreckages[i];
            if (battleWreckage.IsWreckageDestroyable && !encounteredWreckages.Contains(battleWreckage))
            {
                battleWreckage.DestroyWreckage();
            }
        }

        // Like vanilla, an investigation expires and frees its wreckage a day after its last interrupt, even if it was resumed
        foreach (var encounter in encounterTracker.GetExpiredEncounters(CampaignTime.Now))
        {
            LeaveWreckageEncounter(encounter.Hero, BattleWreckageCampaignBehavior.WreckageLeaveReason.WreckageExpired);
        }
    }

    public void OnPartyEncounter(PartyBase attackerParty, PartyBase defenderParty)
    {
        // Only players are tracked, so a tracked leader hero is a player being attacked mid investigation
        var hero = defenderParty.LeaderHero;
        if (hero == null || !encounterTracker.TryGetEncounter(hero, out _)) return;

        LeaveWreckageEncounter(hero, BattleWreckageCampaignBehavior.WreckageLeaveReason.PlayerInterrupted);
    }

    public void OnHeroPrisonerTakenEvent(Hero hero)
    {
        if (hero == null || !encounterTracker.TryGetEncounter(hero, out _)) return;

        LeaveWreckageEncounter(hero, BattleWreckageCampaignBehavior.WreckageLeaveReason.WreckageExpired);
    }

    public void LeaveWreckageEncounter(Hero mainHero, BattleWreckageCampaignBehavior.WreckageLeaveReason reason)
    {
        if (!encounterTracker.TryGetEncounter(mainHero, out var encounter))
        {
            Logger.Debug("No wreckage encounter for {Hero} to leave with {Reason}", mainHero.StringId, reason);
            return;
        }

        var requiredInvestigationDuration = CampaignTime.Zero;

        // World half of vanilla LeaveWreckageEncounter
        switch (reason)
        {
            case BattleWreckageCampaignBehavior.WreckageLeaveReason.InvestigationCompleted:
                encounter.BattleWreckage.OnWreckageInvestigated();
                if (encounter.BattleWreckage.WreckageTypeCategory != BattleWreckage.WreckageType.Epic)
                {
                    encounter.BattleWreckage.DestroyWreckage();
                }
                break;
            case BattleWreckageCampaignBehavior.WreckageLeaveReason.AbandonInvestigation:
                if (mainHero.PartyBelongedTo is MobileParty party && !party.IsDisorganized)
                {
                    party.SetDisorganized(isDisorganized: true);
                }
                break;
            case BattleWreckageCampaignBehavior.WreckageLeaveReason.PlayerInterrupted:
                encounterTracker.Interrupt(encounter, CampaignTime.Now);
                requiredInvestigationDuration = encounter.RequiredInvestigationDuration;
                break;
        }

        // An interrupted investigation keeps its encounter so it can resume
        if (reason != BattleWreckageCampaignBehavior.WreckageLeaveReason.PlayerInterrupted)
        {
            encounterTracker.RemoveEncounter(mainHero);
        }

        var message = new WreckageEncounterLeft(mainHero, reason, requiredInvestigationDuration);
        messageBroker.Publish(this, message);
    }

    public void CloseWreckageEncounter(
        BattleWreckageCampaignBehavior.WreckageLeaveReason reason,
        CampaignTime requiredInvestigationDuration)
    {
        // Local half of vanilla LeaveWreckageEncounter, the server marks the wreckage investigated, removes it and disorganises the party
        if (!TryGetBattleWreckageBehavior(out var behavior)) return;

        if (PlayerEncounter.Current == null && Campaign.Current.CurrentMenuContext != null)
        {
            GameMenu.ExitToLast();
        }

        // Roster writes are patched, and these rosters are the client's own copy of the server's results
        using (new AllowedThread())
        {
            behavior._lootedItems.Clear();
            behavior._lootedTroops.Clear();
        }

        behavior._playerInvestigationStartTime = CampaignTime.Zero;
        behavior._selectedConsequences.Clear();
        behavior._lootedGoldAmount = 0;
        behavior._consequenceExplanations.Clear();
        // Zero unless interrupted, when it's the time left that the server worked out
        behavior._requiredInvestigationDuration = requiredInvestigationDuration;

        // An interrupted investigation keeps its wreckage so it can resume
        if (reason == BattleWreckageCampaignBehavior.WreckageLeaveReason.PlayerInterrupted) return;

        behavior._encounteredBattleWreckage = null;
        behavior._recoverTroopCountsToAdd = 0;
        behavior._currentOverriddenConsequenceId = string.Empty;
        behavior._isOverriddenConsequenceAccepted = false;
    }

    public void ShowWreckageInvestigationResults(
        TroopRosterData lootedTroops,
        IEnumerable<ItemRosterElement> lootedItems,
        IEnumerable<TextObject> consequenceExplanations,
        int lootedGoldAmount,
        Settlement foundHideout)
    {
        if (!TryGetBattleWreckageBehavior(out var behavior)) return;

        // The rest of vanilla GainGoldConsequence's hideout reveal, IsVisible is synced and the spotted notification is relayed
        if (foundHideout != null)
        {
            Campaign.Current.VisualTrackerManager.RegisterObject(foundHideout);
        }

        // Roster writes are patched, and these rosters are the client's own copy of the server's results
        using (new AllowedThread())
        {
            troopRosterInterface.UpdateWithData(behavior._lootedTroops, lootedTroops, mainHero: null);

            behavior._lootedItems.Clear();
            behavior._lootedItems.Add(lootedItems);
        }

        behavior._consequenceExplanations.Clear();
        behavior._consequenceExplanations.AddRange(consequenceExplanations);

        // The server already paid the gold and GiveGoldAction doesn't run on clients, so vanilla only shows the message
        behavior._lootedGoldAmount = lootedGoldAmount;

        // Same as the end of vanilla SelectWreckageConsequences. The results are loaded first, so the connection menu's init opens
        // the troops screen, then the items screen, then the results menu as it reopens, and its placeholder text never shows
        GameMenu.SwitchToMenu(BattleWreckageCampaignBehavior.BattleWreckageResultsConnectionMenuId);
    }

    public void SelectWreckageConsequences(Hero mainHero)
    {
        if (!encounterTracker.TryGetEncounter(mainHero, out var encounter))
        {
            Logger.Debug("No wreckage encounter for {Hero} to select consequences for", mainHero.StringId);
            return;
        }

        // A second request, like a double-click on the recover troops decline, would pay out a second roll
        if (!encounter.IsInvestigating)
        {
            Logger.Debug("Wreckage encounter for {Hero} has no running investigation to select consequences for", mainHero.StringId);
            return;
        }

        if (!TryGetBattleWreckageBehavior(out var behavior)) return;

        if (!TryRollResults(behavior, encounter, out var results))
        {
            var decision = new RecoverTroopsDecisionRequired(mainHero, encounter.RecoverTroopCountsToAdd, encounter.RequiredInvestigationDuration);
            messageBroker.Publish(this, decision);
            return;
        }

        encounter.IsInvestigating = false;
        ApplyWreckageInvestigationResults(results);
    }

    public void ShowRecoverTroopsDecision(int recoverTroopCount, CampaignTime requiredInvestigationDuration)
    {
        if (!TryGetBattleWreckageBehavior(out var behavior)) return;

        // Vanilla's wreckage menu reads the override id to resume an interrupted recovery
        behavior._currentOverriddenConsequenceId = BattleWreckageCampaignBehavior.RecoverTroopsConsequenceId;
        behavior._recoverTroopCountsToAdd = recoverTroopCount;
        behavior._requiredInvestigationDuration = requiredInvestigationDuration;

        GameMenu.SwitchToMenu(BattleWreckageCampaignBehavior.MenuIdRecoverTroopsDecision);
    }

    public void AcceptRecoverTroops(Hero mainHero)
    {
        if (!encounterTracker.TryGetEncounter(mainHero, out var encounter))
        {
            Logger.Debug("No wreckage encounter for {Hero} to accept recover troops for", mainHero.StringId);
            return;
        }

        // Same as vanilla game_menu_recover_troops_accept_consequence, the next roll then applies the recovered troops
        encounter.IsOverriddenConsequenceAccepted = true;
        encounter.InvestigationStartTime = CampaignTime.Now;
    }

    public void InterruptDisconnectedPlayers()
    {
        foreach (var encounter in encounterTracker.Encounters)
        {
            // An interrupted encounter keeps the time it has left, interrupting it again would count from CampaignTime.Zero
            if (!encounter.IsInvestigating) continue;
            if (!playerManager.IsOwnerOfHeroDisconnected(encounter.Hero)) continue;

            // Kept like being attacked mid investigation, so the player resumes on reconnect until it expires
            LeaveWreckageEncounter(encounter.Hero, BattleWreckageCampaignBehavior.WreckageLeaveReason.PlayerInterrupted);
        }
    }

    // One player investigates a wreckage at a time, and an interrupted one keeps it for their resume unless they disconnected.
    // Investigated Epic remains only show the battle summary, so any number of players can view them
    private bool TryTakeWreckage(BattleWreckage battleWreckage, Hero mainHero)
    {
        var isRemains = battleWreckage.IsInvestigated && battleWreckage.WreckageTypeCategory == BattleWreckage.WreckageType.Epic;
        if (isRemains) return true;

        var holders = encounterTracker.Encounters.Where(other => other.Hero != mainHero && other.BattleWreckage == battleWreckage).ToList();
        var isHeldByConnectedPlayer = holders.Any(holder => !playerManager.IsOwnerOfHeroDisconnected(holder.Hero));
        if (isHeldByConnectedPlayer)
        {
            messageBroker.Publish(this, new WreckageEncounterDenied(mainHero));
            return false;
        }

        // A disconnected player's interrupted investigation ends early so whoever comes next can take the wreckage
        foreach (var holder in holders)
        {
            LeaveWreckageEncounter(holder.Hero, BattleWreckageCampaignBehavior.WreckageLeaveReason.WreckageExpired);
        }

        return true;
    }

    // Server half of vanilla ApplyWreckageInvestigationResults, sent along with the roll so the client
    // can open its results screens straight away
    private void ApplyWreckageInvestigationResults(WreckageInvestigationResultsApplied results)
    {
        if (results.LootedGoldAmount > 0)
        {
            GiveGoldAction.ApplyBetweenCharacters(null, results.MainHero, results.LootedGoldAmount, disableNotification: true);
        }

        messageBroker.Publish(this, results);
    }

    // Vanilla GetWreckageInvestigationDuration reads the encountered wreckage, MainHero and MainParty, which the server
    // doesn't have, so the player's wreckage and party stand in while it runs
    private static CampaignTime GetInvestigationDuration(BattleWreckageCampaignBehavior behavior, BattleWreckage battleWreckage, Hero mainHero)
    {
        using (new MainHeroSubstitutionScope(mainHero, mainHero.PartyBelongedTo))
        {
            behavior._encounteredBattleWreckage = battleWreckage;
            try
            {
                return CampaignTime.Hours(behavior.GetWreckageInvestigationDuration(isInterruptedWreckage: false).ResultNumber);
            }
            finally
            {
                behavior._encounteredBattleWreckage = null;
            }
        }
    }

    // Vanilla consequences roll into the behavior's single player fields and read MainParty and MainHero, so the player's
    // encounter stands in for them while they run. Returns false when recover troops was rolled and the player has to decide first
    private static bool TryRollResults(
        BattleWreckageCampaignBehavior behavior,
        BattleWreckageEncounter encounter,
        out WreckageInvestigationResultsApplied results)
    {
        using (new MainHeroSubstitutionScope(encounter.Hero, encounter.Hero.PartyBelongedTo))
        {
            behavior._encounteredBattleWreckage = encounter.BattleWreckage;
            behavior._currentOverriddenConsequenceId = encounter.OverriddenConsequenceId;
            behavior._isOverriddenConsequenceAccepted = encounter.IsOverriddenConsequenceAccepted;
            behavior._recoverTroopCountsToAdd = encounter.RecoverTroopCountsToAdd;
            behavior._requiredInvestigationDuration = encounter.RequiredInvestigationDuration;

            // Plain rosters aren't registered, so the loot stays off the network until the results message sends it
            var behaviorLootedItems = behavior._lootedItems;
            var behaviorLootedTroops = behavior._lootedTroops;
            var behaviorConsequenceExplanations = behavior._consequenceExplanations;
            behavior._lootedItems = new ItemRoster();
            behavior._lootedTroops = new TroopRoster();
            behavior._consequenceExplanations = new List<TextObject>();
            behavior._lootedGoldAmount = 0;

            // Vanilla GainGoldConsequence can reveal one of these and pin it, which on the server pins it on the server's own map
            var hiddenHideouts = Hideout.All.Where(hideout => !hideout.Settlement.IsVisible).ToList();

            try
            {
                var hasResults = TryRollWreckageConsequences(behavior);

                // Only what the roll writes goes back to the encounter
                encounter.OverriddenConsequenceId = behavior._currentOverriddenConsequenceId;
                encounter.RecoverTroopCountsToAdd = behavior._recoverTroopCountsToAdd;
                encounter.RequiredInvestigationDuration = behavior._requiredInvestigationDuration;

                results = new WreckageInvestigationResultsApplied(
                    encounter.Hero,
                    behavior._lootedTroops,
                    behavior._lootedItems,
                    behavior._consequenceExplanations,
                    behavior._lootedGoldAmount,
                    hiddenHideouts.FirstOrDefault(hideout => hideout.Settlement.IsVisible)?.Settlement);

                return hasResults;
            }
            finally
            {
                // The host never encounters a wreckage itself, and these fields go into the server's save
                behavior._encounteredBattleWreckage = null;
                behavior._currentOverriddenConsequenceId = string.Empty;
                behavior._isOverriddenConsequenceAccepted = false;
                behavior._recoverTroopCountsToAdd = 0;
                behavior._requiredInvestigationDuration = CampaignTime.Zero;
                behavior._lootedItems = behaviorLootedItems;
                behavior._lootedTroops = behaviorLootedTroops;
                behavior._consequenceExplanations = behaviorConsequenceExplanations;
                behavior._lootedGoldAmount = 0;
                behavior._selectedConsequences.Clear();
            }
        }
    }

    // Vanilla SelectWreckageConsequences without its menu switches, which stop campaign time on the server.
    // Returns false when recover troops was rolled and the player has to decide on it first
    private static bool TryRollWreckageConsequences(BattleWreckageCampaignBehavior behavior)
    {
        var isOverridden = !string.IsNullOrEmpty(behavior._currentOverriddenConsequenceId);
        if (isOverridden && behavior._isOverriddenConsequenceAccepted)
        {
            behavior._selectedConsequences.Add(behavior.FindConsequenceWithId(behavior._currentOverriddenConsequenceId));
        }

        behavior._wreckageConsequences.Shuffle();
        foreach (var consequence in behavior._wreckageConsequences)
        {
            if (consequence.Chance <= 0f) continue;
            if (consequence.CanOverride && isOverridden) continue;
            if (consequence.Condition != null && !consequence.Condition(behavior._encounteredBattleWreckage)) continue;
            if (MBRandom.RandomFloat >= consequence.Chance) continue;

            if (consequence.CanOverride)
            {
                // Recover troops is vanilla's only overriding consequence, and its Consequence would switch to the
                // decision menu, so only the calculation runs here
                behavior._selectedConsequences.Clear();
                behavior._currentOverriddenConsequenceId = consequence.StringId;
                CalculateRecoverTroopsDecision(behavior);
                return false;
            }

            behavior._selectedConsequences.Add(consequence);
        }

        if (behavior._selectedConsequences.Count == 0)
        {
            behavior._selectedConsequences.Add(behavior.FindConsequenceWithId(BattleWreckageCampaignBehavior.NothingFoundConsequenceId));
        }

        behavior.InvokeSelectedConsequences();
        return true;
    }

    // Vanilla CalculateAndShowRecoverTroopsDecision without its menu switch
    private static void CalculateRecoverTroopsDecision(BattleWreckageCampaignBehavior behavior)
    {
        var (minTroopCount, maxTroopCount) = behavior.GetEffectiveWreckageTypeForConsequence() switch
        {
            BattleWreckage.WreckageType.Epic => (10, 15),
            BattleWreckage.WreckageType.Normal => (5, 10),
            _ => (2, 5),
        };

        var recoverTroopCount = MBRandom.RandomInt(minTroopCount, maxTroopCount);
        var effectiveTroopCount = behavior.GetEffectiveTroopCountToRecover(recoverTroopCount);
        behavior._recoverTroopCountsToAdd = recoverTroopCount;
        behavior._requiredInvestigationDuration = CampaignTime.Hours(Math.Max(2, effectiveTroopCount));
    }

    private bool TryGetBattleWreckageBehavior(out BattleWreckageCampaignBehavior battleWreckageBehavior)
    {
        battleWreckageBehavior = Campaign.Current?.GetCampaignBehavior<BattleWreckageCampaignBehavior>();
        if (battleWreckageBehavior != null) return true;

        Logger.Debug("Skipping battle wreckage update because the campaign behavior is unavailable");
        return false;
    }
}
