using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.BattleWreckages.Messages;
using GameInterface.Services.Players;
using GameInterface.Services.TroopRosters.Data;
using GameInterface.Services.TroopRosters.Interfaces;
using Serilog;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.BattleWreckages;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace GameInterface.Services.BattleWreckages.Interfaces;

public interface IBattleWreckageCampaignBehaviorInterface : IGameAbstraction
{
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
        // TODO: Roll the player's consequences server side, publishing RecoverTroopsDecisionRequired when recover troops is rolled, otherwise WreckageConsequencesSelected
    }

    public void ShowRecoverTroopsDecision(int recoverTroopCount, CampaignTime requiredInvestigationDuration)
    {
        // TODO: Client side, set the local troop count and duration and switch to battle_wreckage_recover_troops_decision
    }

    public void ShowWreckageResultsConnectionMenu()
    {
        // TODO: Client side, switch to battle_wreckage_results_connection_menu, whose init steps through the results screens
    }

    public void AcceptRecoverTroops(Hero mainHero)
    {
        // TODO: Mark the player's recover troops consequence as accepted and restart their investigation time
    }

    public void RemoveDisconnectedPlayers()
    {
        foreach (var encounter in encounterTracker.Encounters)
        {
            if (playerManager.IsOwnerOfHeroDisconnected(encounter.Hero))
                encounterTracker.RemoveEncounter(encounter.Hero);
        }
    }

    private bool TryGetBattleWreckageBehavior(out BattleWreckageCampaignBehavior battleWreckageBehavior)
    {
        battleWreckageBehavior = Campaign.Current?.GetCampaignBehavior<BattleWreckageCampaignBehavior>();
        if (battleWreckageBehavior != null) return true;

        Logger.Debug("Skipping battle wreckage update because the campaign behavior is unavailable");
        return false;
    }
}
