using GameInterface.Services.Issues.Framework.Interface;
using System;
using System.IO;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Quests.TheConquestOfSettlement;

using Quest = TheConquestOfSettlementIssueBehavior.TheConquestOfSettlementIssueQuest;

internal sealed class TheConquestOfSettlementQuestSolutionAcceptStrategy : IQuestSolutionAcceptStrategy
{
    private readonly ITheConquestOfSettlementService service;

    public TheConquestOfSettlementQuestSolutionAcceptStrategy(ITheConquestOfSettlementService service)
    {
        this.service = service;
    }

    public byte[] Capture(IssueBase issue)
    {
        if (!(issue.IssueQuest is Quest quest))
        {
            return Array.Empty<byte>();
        }

        service.RecordLocalQuest(quest);
        var startTicks = quest.JournalEntries.Count == 0
            ? CampaignTime.Now.NumTicks
            : quest.JournalEntries[0].LogTime.NumTicks;

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(quest.StringId ?? string.Empty);
        writer.Write(quest.QuestDueTime.NumTicks);
        writer.Write(startTicks);
        return stream.ToArray();
    }

    public void Apply(IssueBase issue, byte[] captured)
    {
        if (!(issue.IssueQuest is Quest quest) || captured == null || captured.Length == 0)
        {
            return;
        }

        using var stream = new MemoryStream(captured);
        using var reader = new BinaryReader(stream);
        var questId = reader.ReadString();
        var dueTicks = reader.ReadInt64();
        var startTicks = reader.ReadInt64();

        using (new Common.Util.AllowedThread())
        {
            quest.StringId = questId;
            quest.ChangeQuestDueTime(new CampaignTime(dueTicks));
            if (quest.JournalEntries.Count != 0)
            {
                quest._journalEntries[0] = new JournalLog(
                    new CampaignTime(startTicks), quest.JournalEntries[0].LogText);
            }
        }

        service.RecordQuest(quest);
    }
}
