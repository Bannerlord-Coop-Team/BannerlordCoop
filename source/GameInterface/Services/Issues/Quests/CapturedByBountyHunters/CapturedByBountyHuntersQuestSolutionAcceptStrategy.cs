using GameInterface.Services.Issues.Framework.Interface;
using System;
using System.IO;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using Quest = TaleWorlds.CampaignSystem.Issues.CapturedByBountyHuntersIssueBehavior.CapturedByBountyHuntersIssueQuest;

namespace GameInterface.Services.Issues.Quests.CapturedByBountyHunters;

/// <summary>
/// The quest's due time comes from each machine's clock at the accept. The accepting player's due time
/// is forced onto the server's quest object.
/// </summary>
internal class CapturedByBountyHuntersQuestSolutionAcceptStrategy : IQuestSolutionAcceptStrategy
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
                writer.Write(quest.QuestDueTime.NumTicks);
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
                quest.QuestDueTime = new CampaignTime(reader.ReadInt64());
            }
        }
    }
}
