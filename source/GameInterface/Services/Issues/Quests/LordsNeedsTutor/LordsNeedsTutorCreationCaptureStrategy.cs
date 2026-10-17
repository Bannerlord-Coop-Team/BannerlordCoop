using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using System;
using System.IO;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using Issue = TaleWorlds.CampaignSystem.Issues.LordsNeedsTutorIssueBehavior.LordsNeedsTutorIssue;

namespace GameInterface.Services.Issues.Quests.LordsNeedsTutor;

internal sealed class LordsNeedsTutorCreationCaptureStrategy : ICreationCaptureStrategy
{
    private readonly IObjectManager objectManager;
    public LordsNeedsTutorCreationCaptureStrategy(IObjectManager objectManager) => this.objectManager = objectManager;

    public byte[] Capture(IssueBase issue)
    {
        return CapturePupil(((Issue)issue)._youngHero);
    }

    private byte[] CapturePupil(Hero pupil)
    {
        objectManager.TryGetIdWithLogging(pupil, out var pupilId);
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(pupilId ?? string.Empty);
        return stream.ToArray();
    }

    public IssueBase CreateIssue(Hero issueOwner, byte[] captured)
    {
        using var reader = new BinaryReader(new MemoryStream(captured));
        var pupilId = reader.ReadString();
        objectManager.TryGetObjectWithLogging<Hero>(pupilId, out var pupil);
        return new Issue(issueOwner, pupil);
    }

    public void ApplyAfterCreation(IssueBase issue, byte[] captured) { }

    public bool TryBuildDebugCapture(Hero issueOwner, out byte[] captured)
    {
        captured = null;
        var behavior = Campaign.Current.GetCampaignBehavior<LordsNeedsTutorIssueBehavior>();
        if (behavior == null || !behavior.ConditionsHold(issueOwner, out var pupil)) return false;
        captured = CapturePupil(pupil);
        return true;
    }
}
