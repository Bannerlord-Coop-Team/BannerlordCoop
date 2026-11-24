using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.BattleWreckages;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Localization;

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
    public bool IsInterrupted { get; set; }
    // False while interrupted, when the encounter only holds the time left for a resume
    public bool IsInvestigating { get; set; }
    public string OverriddenConsequenceId { get; set; }
    public bool IsOverriddenConsequenceAccepted { get; set; }
    public int RecoverTroopCountsToAdd { get; set; }
    public ItemRoster LootedItems { get; set; }
    public TroopRoster LootedTroops { get; set; }
    public int LootedGoldAmount { get; set; }
    public List<TextObject> ConsequenceExplanations { get; set; }
}
