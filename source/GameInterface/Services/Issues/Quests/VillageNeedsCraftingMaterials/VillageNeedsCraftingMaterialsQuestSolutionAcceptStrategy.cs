using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using System;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using Quest = TaleWorlds.CampaignSystem.Issues.VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssueQuest;

namespace GameInterface.Services.Issues.Quests.VillageNeedsCraftingMaterials;

/// <summary>
/// The payment comes from the local market price of the material, so the accepting player's
/// payment is forced onto the server's quest object.
/// </summary>
internal class VillageNeedsCraftingMaterialsQuestSolutionAcceptStrategy : IQuestSolutionAcceptStrategy
{
    private static readonly FieldInfo RewardGoldField = AccessTools.Field(typeof(QuestBase), nameof(QuestBase.RewardGold));

    public byte[] Capture(IssueBase issue)
    {
        if (!(issue.IssueQuest is Quest quest))
        {
            return Array.Empty<byte>();
        }

        return BitConverter.GetBytes(quest.RewardGold);
    }

    public void Apply(IssueBase issue, byte[] captured)
    {
        if (!(issue.IssueQuest is Quest quest) || captured.Length < sizeof(int))
        {
            return;
        }

        RewardGoldField.SetValue(quest, BitConverter.ToInt32(captured, 0));
    }
}
