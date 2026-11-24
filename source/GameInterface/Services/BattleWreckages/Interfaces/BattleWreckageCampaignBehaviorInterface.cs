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
    void OnHourlyTick();
    void OnPartyEncounter(PartyBase attackerParty, PartyBase defenderParty);
    void OnHeroPrisonerTakenEvent(Hero hero);
    void LeaveWreckageEncounter(Hero mainHero, BattleWreckageCampaignBehavior.WreckageLeaveReason reason);
    void CloseWreckageEncounter(
        BattleWreckageCampaignBehavior.WreckageLeaveReason reason,
        CampaignTime requiredInvestigationDuration);
    void ApplyWreckageInvestigationResults(Hero mainHero);
    void ShowWreckageInvestigationResults(
        TroopRosterData lootedTroops,
        IEnumerable<ItemRosterElement> lootedItems,
        IEnumerable<TextObject> consequenceExplanations,
        int lootedGoldAmount);
    void SelectWreckageConsequences(Hero mainHero);
    void ShowRecoverTroopsDecision(int recoverTroopCount, CampaignTime requiredInvestigationDuration);
    void ShowWreckageResultsConnectionMenu();
    void AcceptRecoverTroops(Hero mainHero);
    void RemoveDisconnectedPlayers();
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

        encounter.IsInterrupted = isInterrupted;
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

    public void OnHourlyTick()
    {
        // The server has no encountered wreckage or visibility, so tracked encounters are what keep a wreckage alive
        var encounteredWreckages = encounterTracker.Encounters.Select(encounter => encounter.BattleWreckage).ToList();

        // Destroy expired wreckages
        for (int i = Campaign.Current.Wreckages.Count - 1; i >= 0; i--)
        {
            var battleWreckage = Campaign.Current.Wreckages[i];
            if (battleWreckage.IsWreckageDestroyable && !encounteredWreckages.Contains(battleWreckage))
            {
                battleWreckage.DestroyWreckage();
            }
        }

        // Check active players in wreckages and make them leave
        foreach (var encounter in encounterTracker.Encounters)
        {
            if (encounter.LastInteractionTime != CampaignTime.Zero &&
                (CampaignTime.Now - encounter.LastInteractionTime).ToHours > BattleWreckageCampaignBehavior.InterruptedSnapshotExpiryAsHours)
            {
                LeaveWreckageEncounter(encounter.Hero, BattleWreckageCampaignBehavior.WreckageLeaveReason.WreckageExpired);
            }
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
                var elapsed = CampaignTime.Now - encounter.InvestigationStartTime;
                var remainingHours = MathF.Max(1f, (float)(encounter.RequiredInvestigationDuration.ToHours - elapsed.ToHours));
                encounter.RequiredInvestigationDuration = CampaignTime.Hours(remainingHours);
                encounter.LastInteractionTime = CampaignTime.Now;
                encounter.InvestigationStartTime = CampaignTime.Zero;
                encounter.LootedTroops = null;
                encounter.LootedItems = null;
                encounter.ConsequenceExplanations = null;
                encounter.LootedGoldAmount = 0;
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
        // ApplyWreckageInvestigationResultsPrefix runs vanilla while this has entries, so the next investigation would replay old results
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

    public void ApplyWreckageInvestigationResults(Hero mainHero)
    {
        if (!encounterTracker.TryGetEncounter(mainHero, out var encounter)) return;

        // Nothing rolled yet, or already sent to the client by an earlier request
        if (encounter.ConsequenceExplanations == null || encounter.ConsequenceExplanations.Count == 0) return;

        if (encounter.LootedGoldAmount > 0)
        {
            GiveGoldAction.ApplyBetweenCharacters(null, mainHero, encounter.LootedGoldAmount, disableNotification: true);
        }

        var message = new WreckageInvestigationResultsApplied(
            mainHero,
            encounter.LootedTroops,
            encounter.LootedItems,
            encounter.ConsequenceExplanations,
            encounter.LootedGoldAmount);
        messageBroker.Publish(this, message);

        // The results go to the client once, so a repeated request finds nothing left to send
        encounter.LootedTroops = null;
        encounter.LootedItems = null;
        encounter.ConsequenceExplanations = null;
        encounter.LootedGoldAmount = 0;
    }

    public void ShowWreckageInvestigationResults(
        TroopRosterData lootedTroops,
        IEnumerable<ItemRosterElement> lootedItems,
        IEnumerable<TextObject> consequenceExplanations,
        int lootedGoldAmount)
    {
        if (!TryGetBattleWreckageBehavior(out var behavior)) return;

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

        // Vanilla shows the troops screen, then the items screen, then the results menu as the connection menu reopens
        behavior.ApplyWreckageInvestigationResults();
    }

    public void SelectWreckageConsequences(Hero mainHero)
    {
        if (!encounterTracker.TryGetEncounter(mainHero, out var encounter))
        {
            Logger.Debug("No wreckage encounter for {Hero} to select consequences for", mainHero.StringId);
            return;
        }

        if (!TryGetBattleWreckageBehavior(out var behavior)) return;

        var isRecoverTroopsDecision = RollForEncounter(behavior, encounter);

        if (isRecoverTroopsDecision)
        {
            var decision = new RecoverTroopsDecisionRequired(mainHero, encounter.RecoverTroopCountsToAdd, encounter.RequiredInvestigationDuration);
            messageBroker.Publish(this, decision);
            return;
        }

        messageBroker.Publish(this, new WreckageConsequencesSelected(mainHero));
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

    public void ShowWreckageResultsConnectionMenu()
    {
        // Its init calls ApplyWreckageInvestigationResults, which asks the server for the results
        GameMenu.SwitchToMenu(BattleWreckageCampaignBehavior.BattleWreckageResultsConnectionMenuId);
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

    public void RemoveDisconnectedPlayers()
    {
        foreach (var encounter in encounterTracker.Encounters)
        {
            if (playerManager.IsOwnerOfHeroDisconnected(encounter.Hero))
                encounterTracker.RemoveEncounter(encounter.Hero);
        }
    }

    // Vanilla GetWreckageInvestigationDuration reads the encountered wreckage, MainHero and MainParty, which the server
    // doesn't have, so the player's wreckage and party stand in while it runs
    private static CampaignTime GetInvestigationDuration(BattleWreckageCampaignBehavior behavior, BattleWreckage battleWreckage, Hero mainHero)
    {
        using (new MainHeroSubstitutionScope(mainHero, mainHero.PartyBelongedTo))
        {
            var previousEncounteredBattleWreckage = behavior._encounteredBattleWreckage;
            try
            {
                behavior._encounteredBattleWreckage = battleWreckage;
                return CampaignTime.Hours(behavior.GetWreckageInvestigationDuration(isInterruptedWreckage: false).ResultNumber);
            }
            finally
            {
                behavior._encounteredBattleWreckage = previousEncounteredBattleWreckage;
            }
        }
    }

    // Vanilla consequences roll into the behavior's single player fields and read MainParty and MainHero,
    // so the player's encounter and party stand in for them while they run, then the behavior is put back
    private static bool RollForEncounter(BattleWreckageCampaignBehavior behavior, BattleWreckageEncounter encounter)
    {
        var previousEncounteredBattleWreckage = behavior._encounteredBattleWreckage;
        var previousOverriddenConsequenceId = behavior._currentOverriddenConsequenceId;
        var previousIsOverriddenConsequenceAccepted = behavior._isOverriddenConsequenceAccepted;
        var previousRecoverTroopCountsToAdd = behavior._recoverTroopCountsToAdd;
        var previousRequiredInvestigationDuration = behavior._requiredInvestigationDuration;
        var previousLootedItems = behavior._lootedItems;
        var previousLootedTroops = behavior._lootedTroops;
        var previousLootedGoldAmount = behavior._lootedGoldAmount;
        var previousConsequenceExplanations = behavior._consequenceExplanations;

        using (new MainHeroSubstitutionScope(encounter.Hero, encounter.Hero.PartyBelongedTo))
        {
            try
            {
                behavior._encounteredBattleWreckage = encounter.BattleWreckage;
                behavior._currentOverriddenConsequenceId = encounter.OverriddenConsequenceId;
                behavior._isOverriddenConsequenceAccepted = encounter.IsOverriddenConsequenceAccepted;
                behavior._recoverTroopCountsToAdd = encounter.RecoverTroopCountsToAdd;
                behavior._requiredInvestigationDuration = encounter.RequiredInvestigationDuration;
                // Every roll starts with empty results. Plain rosters aren't registered, so the loot stays off the
                // network until ApplyWreckageInvestigationResults sends it
                behavior._lootedItems = new ItemRoster();
                behavior._lootedTroops = new TroopRoster();
                behavior._lootedGoldAmount = 0;
                behavior._consequenceExplanations = new List<TextObject>();
                behavior._selectedConsequences.Clear();

                var isRecoverTroopsDecision = RollWreckageConsequences(behavior);

                // Only what the roll writes goes back to the encounter
                encounter.OverriddenConsequenceId = behavior._currentOverriddenConsequenceId;
                encounter.RecoverTroopCountsToAdd = behavior._recoverTroopCountsToAdd;
                encounter.RequiredInvestigationDuration = behavior._requiredInvestigationDuration;
                encounter.LootedItems = behavior._lootedItems;
                encounter.LootedTroops = behavior._lootedTroops;
                encounter.LootedGoldAmount = behavior._lootedGoldAmount;
                encounter.ConsequenceExplanations = behavior._consequenceExplanations;

                return isRecoverTroopsDecision;
            }
            finally
            {
                behavior._encounteredBattleWreckage = previousEncounteredBattleWreckage;
                behavior._currentOverriddenConsequenceId = previousOverriddenConsequenceId;
                behavior._isOverriddenConsequenceAccepted = previousIsOverriddenConsequenceAccepted;
                behavior._recoverTroopCountsToAdd = previousRecoverTroopCountsToAdd;
                behavior._requiredInvestigationDuration = previousRequiredInvestigationDuration;
                behavior._lootedItems = previousLootedItems;
                behavior._lootedTroops = previousLootedTroops;
                behavior._lootedGoldAmount = previousLootedGoldAmount;
                behavior._consequenceExplanations = previousConsequenceExplanations;
                behavior._selectedConsequences.Clear();
            }
        }
    }

    // Vanilla SelectWreckageConsequences without its menu switches, which stop campaign time on the server.
    // Returns true when recover troops was rolled and the player has to decide on it first
    private static bool RollWreckageConsequences(BattleWreckageCampaignBehavior behavior)
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
                return true;
            }

            behavior._selectedConsequences.Add(consequence);
        }

        if (behavior._selectedConsequences.Count == 0)
        {
            behavior._selectedConsequences.Add(behavior.FindConsequenceWithId(BattleWreckageCampaignBehavior.NothingFoundConsequenceId));
        }

        behavior.InvokeSelectedConsequences();
        return false;
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
