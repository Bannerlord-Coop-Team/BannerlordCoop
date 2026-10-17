using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using System;
using System.IO;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using Quest = TaleWorlds.CampaignSystem.Issues.VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssueQuest;

namespace GameInterface.Services.Issues.Quests.VillageNeedsCraftingMaterials;

/// <summary>
/// The payment comes from the local market price of the material, so the accepting player's
/// amount and payment are forced onto the server's quest object.
/// </summary>
internal class VillageNeedsCraftingMaterialsQuestSolutionAcceptStrategy : IQuestSolutionAcceptStrategy
{
    private static readonly FieldInfo RequestedItemAmountField = AccessTools.Field(typeof(Quest), nameof(Quest._requestedItemAmount));
    private static readonly FieldInfo RewardGoldField = AccessTools.Field(typeof(QuestBase), nameof(QuestBase.RewardGold));

    public byte[] Capture(IssueBase issue)
    {
        if (!(issue.IssueQuest is Quest quest))
        {
            return Array.Empty<byte>();
        }

        using (var stream = new MemoryStream())
        {
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(quest._requestedItemAmount);
                writer.Write(quest.RewardGold);
                writer.Flush();

                return stream.ToArray();
            }
        }
    }

    public void Apply(IssueBase issue, byte[] captured)
    {
        if (!(issue.IssueQuest is Quest quest) || captured.Length == 0)
        {
            return;
        }

        using (var stream = new MemoryStream(captured))
        {
            using (var reader = new BinaryReader(stream))
            {
                RequestedItemAmountField.SetValue(quest, reader.ReadInt32());
                RewardGoldField.SetValue(quest, reader.ReadInt32());
            }
        }
    }
}
