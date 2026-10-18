using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using System.IO;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.ObjectSystem;
using Issue = TaleWorlds.CampaignSystem.Issues.LesserNobleRevoltIssueBehavior.LesserNobleRevoltIssue;

namespace GameInterface.Services.Issues.Quests.LesserNobleRevolt;

internal sealed class LesserNobleRevoltCreationCaptureStrategy : ICreationCaptureStrategy
{
    private readonly IObjectManager objectManager;

    public LesserNobleRevoltCreationCaptureStrategy(IObjectManager objectManager)
    {
        this.objectManager = objectManager;
    }

    public byte[] Capture(IssueBase issue)
    {
        var revolt = (Issue)issue;
        using (var stream = new MemoryStream())
        using (var writer = new BinaryWriter(stream))
        {
            writer.Write(revolt._lesserNobleName?.ToString() ?? string.Empty);
            writer.Write(revolt.IssueCreationTime.NumTicks);
            writer.Write(revolt.IssueDueTime.NumTicks);
            writer.Flush();
            return stream.ToArray();
        }
    }

    public IssueBase CreateIssue(Hero issueOwner, byte[] captured)
    {
        Read(captured, out var name, out var created, out var due);
        var issue = new Issue(issueOwner)
        {
            _lesserNobleName = new TaleWorlds.Localization.TextObject(name),
            IssueCreationTime = new CampaignTime(created),
            IssueDueTime = new CampaignTime(due),
        };
        return issue;
    }

    public void ApplyAfterCreation(IssueBase issue, byte[] captured)
    {
    }

    public bool TryBuildDebugCapture(Hero issueOwner, out byte[] captured)
    {
        captured = null;
        if (issueOwner.Clan == null || issueOwner.PartyBelongedTo == null)
        {
            return false;
        }

        var issue = new Issue(issueOwner);
        if (!issue.IssueStayAliveConditions())
        {
            return false;
        }

        captured = Capture(issue);
        return true;
    }

    private static void Read(byte[] captured, out string name, out long created, out long due)
    {
        using (var stream = new MemoryStream(captured))
        using (var reader = new BinaryReader(stream))
        {
            name = reader.ReadString();
            created = reader.ReadInt64();
            due = reader.ReadInt64();
        }
    }
}
