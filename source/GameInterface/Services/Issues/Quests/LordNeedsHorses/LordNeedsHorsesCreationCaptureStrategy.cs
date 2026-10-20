using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using Helpers;
using System.IO;
using System.Reflection;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;
using Behavior = TaleWorlds.CampaignSystem.Issues.LordNeedsHorsesIssueBehavior;
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
        using (var writer = new System.IO.BinaryWriter(stream))
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
        using (var reader = new System.IO.BinaryReader(stream))
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

        // Match IssueStayAliveConditions without constructing an Issue. Constructing one on the server
        // registers a live issue and its roster before the real creation path runs.
        if (issueOwner.IsDead || issueOwner.Clan == Clan.PlayerClan ||
            !(issueOwner.IsKingdomLeader || issueOwner.Clan.Leader == issueOwner) ||
            Behavior.ComputeMountsOverInfantryCountRatio(issueOwner.PartyBelongedTo, out _) >= 0.8f)
        {
            return false;
        }

        var mount = MBObjectManager.Instance.GetObjectTypeList<ItemObject>().Where(item => item.IsMountable && item.Culture == issueOwner.Culture &&
            !item.NotMerchandise && item.Tierf > 2f && item.Tierf < 3f).ToList().GetRandomElement() ??
            MBObjectManager.Instance.GetObject<ItemObject>("sumpter_horse");
        var amount = 1 + (int)TaleWorlds.Library.MathF.Ceiling(12f * Campaign.Current.Models.IssueModel.GetIssueDifficultyMultiplier());
        captured = Write(mount?.StringId ?? "sumpter_horse", amount, mount?.Value ?? 0);
        return true;
    }

    private static byte[] Write(string mountId, int amount, int value)
    {
        using var stream = new MemoryStream();
        using var writer = new System.IO.BinaryWriter(stream);
        writer.Write(mountId ?? string.Empty);
        writer.Write(amount);
        writer.Write(value);
        return stream.ToArray();
    }
}
