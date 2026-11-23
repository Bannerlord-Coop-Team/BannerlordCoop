using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using System.IO;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;
using Issue = TaleWorlds.CampaignSystem.Issues.LordNeedsHorsesIssueBehavior.LordNeedsHorsesIssue;

namespace GameInterface.Services.Issues.Quests.LordNeedsHorses;

/// <summary>
/// The constructor picks a random mount and an amount from the creating player's progress, so the server's
/// pick is forced onto every machine's copy of the issue.
/// </summary>
internal class LordNeedsHorsesCreationCaptureStrategy : ICreationCaptureStrategy
{
    private static readonly FieldInfo MountField = AccessTools.Field(typeof(Issue), nameof(Issue._mountObjectToBeDelivered));
    private static readonly FieldInfo MountValueField = AccessTools.Field(typeof(Issue), nameof(Issue._mountValuePerUnit));

    public byte[] Capture(IssueBase issue)
    {
        var horsesIssue = (Issue)issue;

        using (var stream = new MemoryStream())
        {
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(horsesIssue._mountObjectToBeDelivered?.StringId ?? string.Empty);
                writer.Write(horsesIssue._numMountsToBeDelivered);
                writer.Write(horsesIssue._mountValuePerUnit);
                writer.Flush();

                return stream.ToArray();
            }
        }
    }

    public IssueBase CreateIssue(Hero issueOwner, byte[] captured)
    {
        var issue = new Issue(issueOwner);

        using (var stream = new MemoryStream(captured))
        {
            using (var reader = new BinaryReader(stream))
            {
                var mountId = reader.ReadString();

                MountField.SetValue(issue, MBObjectManager.Instance.GetObject<ItemObject>(mountId));
                issue._numMountsToBeDelivered = reader.ReadInt32();
                MountValueField.SetValue(issue, reader.ReadInt32());
            }
        }

        return issue;
    }

    public void ApplyAfterCreation(IssueBase issue, byte[] captured)
    {
    }

    public bool TryBuildDebugCapture(Hero issueOwner, out byte[] captured)
    {
        captured = null;

        if (issueOwner.PartyBelongedTo == null || issueOwner.Clan == null)
        {
            return false;
        }

        // Only rolls the mount and amount, the issue itself is created from the capture
        var issue = new Issue(issueOwner);
        CampaignEventDispatcher.Instance.RemoveListeners(issue);

        // Only a clan or kingdom leader with few mounts keeps the issue alive past the next daily tick
        if (!issue.IssueStayAliveConditions())
        {
            return false;
        }

        captured = Capture(issue);
        return true;
    }
}
