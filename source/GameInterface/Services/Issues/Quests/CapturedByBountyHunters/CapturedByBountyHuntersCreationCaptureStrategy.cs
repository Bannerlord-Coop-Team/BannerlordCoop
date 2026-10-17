using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using System.IO;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Settlements;
using Issue = TaleWorlds.CampaignSystem.Issues.CapturedByBountyHuntersIssueBehavior.CapturedByBountyHuntersIssue;

namespace GameInterface.Services.Issues.Quests.CapturedByBountyHunters;

internal class CapturedByBountyHuntersCreationCaptureStrategy : ICreationCaptureStrategy
{
    private readonly IObjectManager objectManager;

    public CapturedByBountyHuntersCreationCaptureStrategy(IObjectManager objectManager)
    {
        this.objectManager = objectManager;
    }

    public byte[] Capture(IssueBase issue)
    {
        return Write(IdOf(((Issue)issue)._hideout));
    }

    public IssueBase CreateIssue(Hero issueOwner, byte[] captured)
    {
        objectManager.TryGetObjectWithLogging<Settlement>(Read(captured), out var hideout);

        return new Issue(issueOwner, hideout);
    }

    public void ApplyAfterCreation(IssueBase issue, byte[] captured)
    {
    }

    public bool TryBuildDebugCapture(Hero issueOwner, out byte[] captured)
    {
        captured = null;

        if (!issueOwner.IsGangLeader || issueOwner.CurrentSettlement == null)
        {
            return false;
        }

        var hideouts = Campaign.Current.AllHideouts;
        var hideout = hideouts.FirstOrDefault(candidate => candidate.IsInfested) ?? hideouts.FirstOrDefault();
        if (hideout == null)
        {
            return false;
        }

        captured = Write(IdOf(hideout.Settlement));
        return true;
    }

    private string IdOf(object obj)
    {
        if (obj == null)
        {
            return string.Empty;
        }

        objectManager.TryGetIdWithLogging(obj, out var id);
        return id ?? string.Empty;
    }

    private static byte[] Write(string hideoutId)
    {
        using (var stream = new MemoryStream())
        {
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(hideoutId);
                writer.Flush();

                return stream.ToArray();
            }
        }
    }

    private static string Read(byte[] captured)
    {
        using (var stream = new MemoryStream(captured))
        {
            using (var reader = new BinaryReader(stream))
            {
                return reader.ReadString();
            }
        }
    }
}
