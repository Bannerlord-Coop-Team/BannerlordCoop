using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using System;
using System.IO;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using Quest = TaleWorlds.CampaignSystem.Issues.HeadmanNeedsGrainIssueBehavior.HeadmanNeedsGrainIssueQuest;

namespace GameInterface.Services.Issues.Quests.HeadmanNeedsGrain;

internal class HeadmanNeedsGrainQuestSolutionAcceptStrategy : IQuestSolutionAcceptStrategy
{
    // Publicizer exposes this field but preserves its readonly modifier.
    private static readonly System.Reflection.FieldInfo NeededAmount = AccessTools.Field(typeof(Quest), nameof(Quest._neededGrainAmount));

    public byte[] Capture(IssueBase issue)
    {
        if (!(issue.IssueQuest is Quest quest)) return Array.Empty<byte>();

        using (var stream = new MemoryStream())
        using (var writer = new BinaryWriter(stream))
        {
            writer.Write(quest._neededGrainAmount);
            writer.Write(quest._rewardGold);
            writer.Write(quest.QuestDueTime.NumTicks);
            return stream.ToArray();
        }
    }

    public void Apply(IssueBase issue, byte[] captured)
    {
        if (!(issue.IssueQuest is Quest quest) || captured.Length == 0) return;

        using (var stream = new MemoryStream(captured))
        using (var reader = new BinaryReader(stream))
        {
            NeededAmount.SetValue(quest, reader.ReadInt32());
            quest._rewardGold = reader.ReadInt32();
            quest.QuestDueTime = new CampaignTime(reader.ReadInt64());
        }
    }
}
