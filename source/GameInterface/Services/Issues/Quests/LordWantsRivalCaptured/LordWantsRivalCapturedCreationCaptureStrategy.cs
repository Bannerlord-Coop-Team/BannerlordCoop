using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.ObjectManager;
using System.IO;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using Issue = TaleWorlds.CampaignSystem.Issues.LordWantsRivalCapturedIssueBehavior.LordWantsRivalCapturedIssue;

namespace GameInterface.Services.Issues.Quests.LordWantsRivalCaptured;

internal sealed class LordWantsRivalCapturedCreationCaptureStrategy : ICreationCaptureStrategy
{
    private readonly IObjectManager objectManager;
    private readonly IIssueGenerationRegistry generations;

    public LordWantsRivalCapturedCreationCaptureStrategy(
        IObjectManager objectManager,
        IIssueGenerationRegistry generations)
    {
        this.objectManager = objectManager;
        this.generations = generations;
    }

    public byte[] Capture(IssueBase issue)
    {
        var rival = (Issue)issue;
        if (!objectManager.TryGetIdWithLogging(rival._targetHero, out var targetId))
        {
            return System.Array.Empty<byte>();
        }

        var generation = generations.Bump(rival.IssueOwner);
        using (var stream = new MemoryStream())
        using (var writer = new BinaryWriter(stream))
        {
            writer.Write(targetId);
            writer.Write(rival.IssueCreationTime.NumTicks);
            writer.Write(rival.IssueDueTime.NumTicks);
            writer.Write(generation);
            writer.Flush();
            return stream.ToArray();
        }
    }

    public IssueBase CreateIssue(Hero issueOwner, byte[] captured)
    {
        Read(captured, out var targetId, out _, out _, out _);
        if (!objectManager.TryGetObjectWithLogging<Hero>(targetId, out var target))
        {
            return null;
        }

        return new Issue(issueOwner, target);
    }

    public void ApplyAfterCreation(IssueBase issue, byte[] captured)
    {
        if (!(issue is Issue rival)) return;

        Read(captured, out _, out var creationTicks, out var dueTicks, out var generation);
        rival.IssueCreationTime = new CampaignTime(creationTicks);
        rival.IssueDueTime = new CampaignTime(dueTicks);
        generations.SetGeneration(rival.IssueOwner, generation);
    }

    public bool TryBuildDebugCapture(Hero issueOwner, out byte[] captured)
    {
        captured = null;
        var target = Hero.AllAliveHeroes.FirstOrDefault(hero =>
            hero != issueOwner && hero.IsLord && hero.MapFaction != issueOwner.MapFaction);
        if (target == null || !objectManager.TryGetId(target, out var targetId)) return false;

        using (var stream = new MemoryStream())
        using (var writer = new BinaryWriter(stream))
        {
            writer.Write(targetId);
            writer.Write(CampaignTime.Now.NumTicks);
            writer.Write(CampaignTime.DaysFromNow(20f).NumTicks);
            writer.Write(0);
            writer.Flush();
            captured = stream.ToArray();
        }
        return true;
    }

    private static void Read(byte[] captured, out string targetId, out long creationTicks, out long dueTicks, out int generation)
    {
        targetId = string.Empty;
        creationTicks = 0;
        dueTicks = 0;
        generation = 0;
        if (captured == null || captured.Length == 0) return;

        using (var stream = new MemoryStream(captured))
        using (var reader = new BinaryReader(stream))
        {
            targetId = reader.ReadString();
            creationTicks = reader.ReadInt64();
            dueTicks = reader.ReadInt64();
            generation = reader.ReadInt32();
        }
    }
}
