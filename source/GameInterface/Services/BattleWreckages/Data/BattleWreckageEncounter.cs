using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.BattleWreckages;

namespace GameInterface.Services.BattleWreckages.Data;

/// <summary>
/// One player's copy of the encounter state vanilla keeps on BattleWreckageCampaignBehavior for the single main hero
/// </summary>
public class BattleWreckageEncounter
{
    public BattleWreckageEncounter(Hero hero, BattleWreckage battleWreckage)
    {
        Hero = hero;
        BattleWreckage = battleWreckage;
    }

    public Hero Hero { get; }
    public BattleWreckage BattleWreckage { get; set; }
    public CampaignTime InvestigationStartTime { get; set; }
    public CampaignTime RequiredInvestigationDuration { get; set; }
    public CampaignTime LastInteractionTime { get; set; }
    // False while interrupted, when the encounter only holds the time left for a resume, and once its results are sent
    public bool IsInvestigating { get; set; }
    public string OverriddenConsequenceId { get; set; }
    public bool IsOverriddenConsequenceAccepted { get; set; }
    public int RecoverTroopCountsToAdd { get; set; }
}
