using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using System.IO;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using Issue = TaleWorlds.CampaignSystem.Issues.LordWantsRivalCapturedIssueBehavior.LordWantsRivalCapturedIssue;

namespace GameInterface.Services.Issues.Quests.LordWantsRivalCaptured;

/// <summary>
/// The server's behavior picks a random rival lord as the target, so its pick is forced onto every machine's copy of the issue.
/// </summary>
internal class LordWantsRivalCapturedCreationCaptureStrategy : ICreationCaptureStrategy
{
    private readonly IObjectManager objectManager;

    public LordWantsRivalCapturedCreationCaptureStrategy(IObjectManager objectManager)
    {
        this.objectManager = objectManager;
    }

    public byte[] Capture(IssueBase issue)
    {
        var target = ((Issue)issue)._targetHero;

        objectManager.TryGetIdWithLogging(target, out var targetId);
        return Write(targetId ?? string.Empty);
    }

    public IssueBase CreateIssue(Hero issueOwner, byte[] captured)
    {
        objectManager.TryGetObjectWithLogging<Hero>(Read(captured), out var target);

        return new Issue(issueOwner, target);
    }

    public void ApplyAfterCreation(IssueBase issue, byte[] captured)
    {
    }

    public bool TryBuildDebugCapture(Hero issueOwner, out byte[] captured)
    {
        captured = null;

        if (issueOwner.MapFaction == null)
        {
            return false;
        }

        var target = Hero.AllAliveHeroes.FirstOrDefault(hero =>
            hero.IsLord &&
            hero.PartyBelongedTo != null &&
            hero.MapFaction != null &&
            hero.MapFaction.IsAtWarWith(issueOwner.MapFaction));

        if (target == null || !objectManager.TryGetIdWithLogging(target, out var targetId))
        {
            return false;
        }

        captured = Write(targetId);
        return true;
    }

    private static byte[] Write(string targetId)
    {
        using (var stream = new MemoryStream())
        {
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(targetId);
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
