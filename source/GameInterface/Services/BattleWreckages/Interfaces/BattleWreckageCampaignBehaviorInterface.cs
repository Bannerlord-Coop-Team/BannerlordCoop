using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.BattleWreckages.Interfaces;

public interface IBattleWreckageCampaignBehaviorInterface : IGameAbstraction
{
    void OnHourlyTick();
    void OnPartyEncounter(PartyBase attackerParty, PartyBase defenderParty);
    void OnHeroPrisonerTakenEvent(Hero hero);
    void LeaveWreckageEncounter(Hero mainHero, BattleWreckageCampaignBehavior.WreckageLeaveReason reason);
    void CloseWreckageEncounter(BattleWreckageCampaignBehavior.WreckageLeaveReason reason);
    void ApplyWreckageInvestigationResults(Hero mainHero);
    void ShowWreckageInvestigationResults();
    void SelectWreckageConsequences(Hero mainHero);
    void ShowRecoverTroopsDecision(int recoverTroopCount, CampaignTime requiredInvestigationDuration);
    void ShowWreckageResultsConnectionMenu();
    void AcceptRecoverTroops(Hero mainHero);
}

public class BattleWreckageCampaignBehaviorInterface : IBattleWreckageCampaignBehaviorInterface
{
    private readonly IBattleWreckageEncounterTracker encounterTracker;

    public BattleWreckageCampaignBehaviorInterface(IBattleWreckageEncounterTracker encounterTracker)
    {
        this.encounterTracker = encounterTracker;
    }

    public void OnHourlyTick()
    {
        // TODO: Destroy expired wreckages, then check active players in wreckages and make them leave
    }

    public void OnPartyEncounter(PartyBase attackerParty, PartyBase defenderParty)
    {
        // TODO: Check if the defender is a player and make them leave their wreckage encounter
    }

    public void OnHeroPrisonerTakenEvent(Hero hero)
    {
        // TODO: Check if the taken hero is a player and make them leave their wreckage encounter
    }

    public void LeaveWreckageEncounter(Hero mainHero, BattleWreckageCampaignBehavior.WreckageLeaveReason reason)
    {
        // TODO: Clear the player's encounter and publish WreckageEncounterLeft. Use vanilla OnWreckageInvestigated so AutoSync sends _isInvestigated
    }

    public void CloseWreckageEncounter(BattleWreckageCampaignBehavior.WreckageLeaveReason reason)
    {
        // TODO: Client side, clear the local encounter and exit the wreckage menu
    }

    public void ApplyWreckageInvestigationResults(Hero mainHero)
    {
        // TODO: Apply the results server side and publish WreckageInvestigationResultsApplied
    }

    public void ShowWreckageInvestigationResults()
    {
        // TODO: Client side, open the party screen, inventory screen or results menu
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
}
