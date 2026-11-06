using Common;
using Common.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using System;
using System.Collections.Generic;
using System.IO;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;
using Quest = TaleWorlds.CampaignSystem.Issues.RevenueFarmingIssueBehavior.RevenueFarmingIssueQuest;

namespace GameInterface.Services.Issues.Quests.RevenueFarming;

internal sealed class RevenueFarmingQuestSolutionAcceptStrategy : IQuestSolutionAcceptStrategy
{
    private readonly IObjectManager objects;
    private readonly IRevenueFarmingSessionStore sessions;
    private readonly IControllerIdProvider controller;
    private readonly IIssueOwnerResolver owners;

    public RevenueFarmingQuestSolutionAcceptStrategy(IObjectManager objects, IRevenueFarmingSessionStore sessions,
        IControllerIdProvider controller, IIssueOwnerResolver owners)
    {
        this.objects = objects;
        this.sessions = sessions;
        this.controller = controller;
        this.owners = owners;
    }

    public byte[] Capture(IssueBase issue)
    {
        if (issue.IssueQuest is not Quest quest) return Array.Empty<byte>();
        sessions.Register(quest, Hero.MainHero, controller.ControllerId);
        using (new AllowedThread()) quest.StringId = issue.StringId + "_quest";
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(quest.StringId);
        writer.Write(quest.QuestDueTime.NumTicks);
        writer.Write(quest.RevenueVillages.Count);
        foreach (var village in quest.RevenueVillages)
        {
            if (!objects.TryGetIdWithLogging(village.Village.Settlement, out var id))
                throw new InvalidOperationException("Revenue Farming village is not registered.");
            writer.Write(id);
            writer.Write(village.TargetAmount);
        }
        return stream.ToArray();
    }

    public void Apply(IssueBase issue, byte[] captured)
    {
        // Other players have an issue without a quest, so no personal state is created for them.
        if (issue.IssueQuest is not Quest quest) return;
        using var stream = new MemoryStream(captured);
        using var reader = new BinaryReader(stream);
        var questId = reader.ReadString();
        var dueTime = new CampaignTime(reader.ReadInt64());
        var count = reader.ReadInt32();
        if (count <= 0 || count > ((RevenueFarmingIssueBehavior.RevenueFarmingIssue)issue)._targetSettlement.BoundVillages.Count)
            throw new InvalidDataException("Revenue Farming acceptance has no valid village list.");
        var villages = new List<RevenueFarmingIssueBehavior.RevenueVillage>();
        var total = 0;
        for (var i = 0; i < count; i++)
        {
            var id = reader.ReadString();
            var target = reader.ReadInt32();
            if (target < 0 || !objects.TryGetObjectWithLogging<Settlement>(id, out var settlement) ||
                settlement.Village?.Bound != ((RevenueFarmingIssueBehavior.RevenueFarmingIssue)issue)._targetSettlement ||
                villages.Exists(village => village.Village == settlement.Village))
                throw new InvalidDataException("Revenue Farming acceptance has an invalid village.");
            villages.Add(new RevenueFarmingIssueBehavior.RevenueVillage(settlement.Village, target));
            total += target / 3;
        }
        if (!owners.TryResolveOwnerHero(issue, out var hero) || !owners.TryGetOwnerControllerId(issue, out var owner))
            throw new InvalidOperationException("Revenue Farming acceptance has no owning player.");
        quest.RevenueVillages.Clear();
        quest.RevenueVillages.AddRange(villages);
        quest._totalRequestedDenars = total;
        quest.QuestDueTime = dueTime;
        using (new AllowedThread()) quest.StringId = questId;
        var session = sessions.Register(quest, hero, owner);
        // The server keeps the journal needed for progress replay without starting its quest or registering listeners.
        using (new MainHeroSubstitutionScope(hero, hero.PartyBelongedTo))
            quest._questProgressLog = quest.AddDiscreteLog(quest.QuestStartedLogText,
                new TextObject("{=bC5aMfG0}Villages"), 0, villages.Count, null, hideInformation: true);
        foreach (var village in villages)
        {
            if (!village.Village.Settlement.IsUnderRaid && !village.Village.Settlement.IsRaided) continue;
            using (new RevenueFarmingExecutionScope(session)) quest.OnVillageRaid(village.Village);
        }
    }
}
