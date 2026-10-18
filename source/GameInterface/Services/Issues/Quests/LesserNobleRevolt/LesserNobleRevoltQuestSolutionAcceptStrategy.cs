using Common;
using Common.Network;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using Quest = TaleWorlds.CampaignSystem.Issues.LesserNobleRevoltIssueBehavior.LesserNobleRevoltIssueQuest;

namespace GameInterface.Services.Issues.Quests.LesserNobleRevolt;

internal sealed class LesserNobleRevoltQuestSolutionAcceptStrategy : IQuestSolutionAcceptStrategy
{
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly IIssueOwnerResolver ownerResolver;
    private readonly IIssueOwnershipRegistry ownership;

    public LesserNobleRevoltQuestSolutionAcceptStrategy(
        IObjectManager objectManager,
        INetwork network,
        IIssueOwnerResolver ownerResolver,
        IIssueOwnershipRegistry ownership)
    {
        this.objectManager = objectManager;
        this.network = network;
        this.ownerResolver = ownerResolver;
        this.ownership = ownership;
    }

    public byte[] Capture(IssueBase issue)
    {
        if (!(issue.IssueQuest is Quest quest))
        {
            return Array.Empty<byte>();
        }

        var capturedParty = quest._lesserNobleParty;
        if (capturedParty != null && capturedParty == MobileParty.MainParty) capturedParty = null;
        return Write(
            Id(capturedParty),
            quest.QuestDueTime.NumTicks,
            quest._persuasionTriedOnce,
            quest._firstTalkIsDone,
            quest._checkForEventEnd,
            quest._goOutFromSettlementAfterHoursCounter,
            quest._giveNotificationAfterHoursCounter,
            quest._suitableVillagesToVisitList,
            quest._visitedVillagesList);
    }

    public void Apply(IssueBase issue, byte[] captured)
    {
        if (!(issue.IssueQuest is Quest quest) || captured == null || captured.Length == 0)
        {
            return;
        }

        // Only the server applies a capture to a quest object, and it keeps its own rewards.
        Read(captured, out var nobleId, out var due, out var persuasion,
            out var firstTalk, out var waiting, out var departure, out var notification, out var suitable, out var visited);

        if (!string.IsNullOrEmpty(nobleId) &&
            objectManager.TryGetObjectWithLogging<MobileParty>(nobleId, out var noble))
        {
            quest._lesserNobleParty = noble;
        }

        quest.QuestDueTime = new CampaignTime(due);
        quest._persuasionTriedOnce = persuasion;
        quest._firstTalkIsDone = firstTalk;
        quest._checkForEventEnd = waiting;
        quest._goOutFromSettlementAfterHoursCounter = departure;
        quest._giveNotificationAfterHoursCounter = notification;
        quest._suitableVillagesToVisitList = Resolve(suitable);
        quest._visitedVillagesList = Resolve(visited);

        if (ModInformation.IsClient || quest._lesserNobleParty == null ||
            !ownerResolver.TryResolveOwnerHero(issue, out _) ||
            !objectManager.TryGetIdWithLogging(issue.IssueOwner, out var issueOwnerId) ||
            !objectManager.TryGetIdWithLogging(quest._lesserNobleParty, out var partyId) ||
            !ownership.TryGetOwner(issueOwnerId, issue.StringId, out var controllerId))
        {
            return;
        }

        network.SendAll(new NetworkLesserNobleRevoltPartyCreated(issueOwnerId, issue.StringId, controllerId, partyId));
    }

    private string Id(object value)
    {
        return value != null && objectManager.TryGetIdWithLogging(value, out var id) ? id : string.Empty;
    }

    private string[] Ids(IEnumerable<Settlement> values)
    {
        return values?.Select(Id).Where(id => !string.IsNullOrEmpty(id)).ToArray() ?? Array.Empty<string>();
    }

    private List<Settlement> Resolve(string[] ids)
    {
        return (ids ?? Array.Empty<string>())
            .Select(id => objectManager.TryGetObjectWithLogging<Settlement>(id, out var settlement) ? settlement : null)
            .Where(settlement => settlement != null)
            .ToList();
    }

    private byte[] Write(
        string nobleId,
        long due,
        bool persuasion,
        bool firstTalk,
        bool waiting,
        int departure,
        int notification,
        IEnumerable<Settlement> suitable,
        IEnumerable<Settlement> visited)
    {
        using (var stream = new MemoryStream())
        using (var writer = new BinaryWriter(stream))
        {
            writer.Write(nobleId);
            writer.Write(due);
            writer.Write(persuasion);
            writer.Write(firstTalk);
            writer.Write(waiting);
            writer.Write(departure);
            writer.Write(notification);
            WriteIds(writer, Ids(suitable));
            WriteIds(writer, Ids(visited));
            writer.Flush();
            return stream.ToArray();
        }
    }

    private static void WriteIds(BinaryWriter writer, string[] ids)
    {
        writer.Write(ids.Length);
        foreach (var id in ids) writer.Write(id);
    }

    private static void Read(
        byte[] captured,
        out string nobleId,
        out long due,
        out bool persuasion,
        out bool firstTalk,
        out bool waiting,
        out int departure,
        out int notification,
        out string[] suitable,
        out string[] visited)
    {
        using (var stream = new MemoryStream(captured))
        using (var reader = new BinaryReader(stream))
        {
            nobleId = reader.ReadString();
            due = reader.ReadInt64();
            persuasion = reader.ReadBoolean();
            firstTalk = reader.ReadBoolean();
            waiting = reader.ReadBoolean();
            departure = reader.ReadInt32();
            notification = reader.ReadInt32();
            suitable = ReadIds(reader);
            visited = ReadIds(reader);
        }
    }

    private static string[] ReadIds(BinaryReader reader)
    {
        var count = reader.ReadInt32();
        if (count < 0 || count > 1000) throw new InvalidDataException("Invalid Lesser Noble Revolt settlement count.");
        var ids = new string[count];
        for (var i = 0; i < count; i++) ids[i] = reader.ReadString();
        return ids;
    }
}
