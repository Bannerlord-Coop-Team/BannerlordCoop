using GameInterface.Services.Issues.Framework.Interface;
using System;
using System.IO;
using TaleWorlds.CampaignSystem.Issues;
using Quest = TaleWorlds.CampaignSystem.Issues.ArmyNeedsSuppliesIssueBehavior.ArmyNeedsSuppliesIssueQuest;

namespace GameInterface.Services.Issues.Quests.ArmyNeedsSupplies;

internal class ArmyNeedsSuppliesQuestSolutionAcceptStrategy : IQuestSolutionAcceptStrategy
{
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
                writer.Write(quest._requestedGrainAmount);
                writer.Write(quest._requestedLiveStockAmount);
                writer.Write(quest._requestedWineAmount);
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
                quest._requestedGrainAmount = reader.ReadInt32();
                quest._requestedLiveStockAmount = reader.ReadInt32();
                quest._requestedWineAmount = reader.ReadInt32();
            }
        }
    }
}
