using Common;
using Common.Messaging;
using Common.Network;
using Common.Network.Coalescing;
using Common.Util;
using GameInterface.Serialization;
using GameInterface.Serialization.External;
using GameInterface.Services.Entity;
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
using TaleWorlds.Core;
using TaleWorlds.Localization;
using Quest = TaleWorlds.CampaignSystem.Issues.EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssueQuest;

namespace GameInterface.Services.Issues.Quests.EscortMerchantCaravan;

internal interface IEscortProgress
{
    void Publish(Quest quest);
}

internal sealed class EscortProgress : IEscortProgress
{
    private readonly IObjectManager objects;
    private readonly IIssueOwnerResolver owners;
    private readonly INetwork network;
    private readonly ISendCoalescer coalescer;
    private readonly IBinaryPackageFactory packages;

    public EscortProgress(IObjectManager objects, IIssueOwnerResolver owners, INetwork network,
        ISendCoalescer coalescer, IBinaryPackageFactory packages)
    {
        this.objects = objects;
        this.owners = owners;
        this.network = network;
        this.coalescer = coalescer;
        this.packages = packages;
    }

    public void Publish(Quest quest)
    {
        if (ModInformation.IsClient) return;
        var issue = IssueManager.GetIssueOfQuest(quest);
        if (issue == null || !owners.TryGetOwnerControllerId(issue, out var controller)) return;
        var tracked = Campaign.Current.QuestManager.GetAllTrackedObjectsOfAQuest(quest);
        var state = new NetworkEscortProgress
        {
            GiverId = Id(issue.IssueOwner), IssueId = issue.StringId, ControllerId = controller,
            CaravanId = PartyId(quest._questCaravanMobileParty), BanditId = PartyId(quest._questBanditMobileParty),
            OtherBanditId = PartyId(quest._otherBanditParty), VisitedIds = quest._visitedSettlements.Select(Id).ToArray(),
            Days = quest._daysSpentForEscorting, Warned = quest._isPlayerNotifiedForDanger,
            Journal = CaptureJournal(quest),
            TrackedParties = tracked.OfType<MobileParty>().Where(p => p.IsActive).Select(Id).ToArray(),
            TrackedSettlements = tracked.OfType<Settlement>().Select(Id).ToArray(),
        };
        // Party creation and updates must reach the game-thread queue before the quest references them.
        coalescer.Flush(network);
        network.SendAll(state);
    }

    private string Id(object obj)
    {
        if (obj == null) return null;
        if (!objects.TryGetIdWithLogging(obj, out var id))
            throw new InvalidOperationException("Escort progress references an unregistered object.");
        return id;
    }

    private string PartyId(MobileParty party) => party?.IsActive == true ? Id(party) : null;

    private byte[] CaptureJournal(Quest quest)
    {
        using (var stream = new MemoryStream())
        using (var writer = new BinaryWriter(stream))
        {
            writer.Write(quest.JournalEntries.Count);
            foreach (var log in quest.JournalEntries)
            {
                writer.Write(log.LogTime.NumTicks);
                WriteText(writer, log.LogText);
                WriteText(writer, log.TaskName);
                writer.Write(log.CurrentProgress);
                writer.Write(log.Range);
                writer.Write((int)log.Type);
            }
            return stream.ToArray();
        }
    }

    private void WriteText(BinaryWriter writer, TextObject text)
    {
        var data = text == null ? Array.Empty<byte>() :
            BinaryPackageSerializer.Serialize(packages.GetBinaryPackage<TextObjectBinaryPackage>(text));
        writer.Write(data.Length);
        writer.Write(data);
    }
}

internal sealed class EscortProgressHandler : IHandler
{
    private readonly IMessageBroker messages;
    private readonly IObjectManager objects;
    private readonly IIssueResolver issues;
    private readonly IControllerIdProvider controller;
    private readonly IBinaryPackageFactory packages;

    public EscortProgressHandler(IMessageBroker messages, IObjectManager objects, IIssueResolver issues,
        IControllerIdProvider controller, IBinaryPackageFactory packages)
    {
        this.messages = messages;
        this.objects = objects;
        this.issues = issues;
        this.controller = controller;
        this.packages = packages;
        messages.Subscribe<NetworkEscortProgress>(Receive);
    }

    public void Dispose() => messages.Unsubscribe<NetworkEscortProgress>(Receive);

    private void Receive(MessagePayload<NetworkEscortProgress> payload)
    {
        if (ModInformation.IsServer || payload.What.ControllerId != controller.ControllerId) return;
        var state = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!issues.TryResolve(state.GiverId, state.IssueId, out _, out var issue, out _) ||
                !(issue.IssueQuest is Quest quest) || !quest.IsOngoing) return;

            var caravan = Resolve<MobileParty>(state.CaravanId);
            var bandit = Resolve<MobileParty>(state.BanditId);
            var other = Resolve<MobileParty>(state.OtherBanditId);
            var visited = (state.VisitedIds ?? Array.Empty<string>()).Select(Resolve<Settlement>).ToList();
            var tracked = new HashSet<ITrackableCampaignObject> { quest.QuestGiver };
            foreach (var id in state.TrackedParties ?? Array.Empty<string>()) tracked.Add(Resolve<MobileParty>(id));
            foreach (var id in state.TrackedSettlements ?? Array.Empty<string>()) tracked.Add(Resolve<Settlement>(id));
            var logs = ReadJournal(state.Journal);

            using (new AllowedThread())
            {
                quest._questCaravanMobileParty = caravan;
                quest._questBanditMobileParty = bandit;
                quest._otherBanditParty = other;
                quest._customPartyComponent = null;
                quest._visitedSettlements = visited;
                quest._daysSpentForEscorting = state.Days;
                quest._journalEntries.Clear();
                quest._journalEntries.AddRange(logs);
                quest._playerStartsQuestLog = logs.FirstOrDefault();
                foreach (var previous in Campaign.Current.QuestManager.GetAllTrackedObjectsOfAQuest(quest).ToArray())
                    if (!tracked.Contains(previous)) quest.RemoveTrackedObject(previous);
                foreach (var current in tracked)
                    if (!quest.IsTracked(current)) quest.AddTrackedObject(current);
                if (state.Warned && !quest._isPlayerNotifiedForDanger)
                    MBInformationManager.AddQuickInformation(new TextObject("{=2y9DhzCR}You are about to lose sight of the caravan. Find the caravan before they are in danger!"));
                quest._isPlayerNotifiedForDanger = state.Warned;
                CampaignEventDispatcher.Instance.OnQuestLogAdded(quest, true);
            }
        }, context: nameof(EscortProgressHandler));
    }

    private T Resolve<T>(string id) where T : class
    {
        if (id == null) return null;
        if (!objects.TryGetObjectWithLogging<T>(id, out var value))
            throw new InvalidOperationException($"Escort progress references missing {typeof(T).Name} {id}.");
        return value;
    }

    private JournalLog[] ReadJournal(byte[] data)
    {
        using (var stream = new MemoryStream(data))
        using (var reader = new BinaryReader(stream))
        {
            var logs = new JournalLog[reader.ReadInt32()];
            for (var i = 0; i < logs.Length; i++)
                logs[i] = new JournalLog(new CampaignTime(reader.ReadInt64()), ReadText(reader), ReadText(reader),
                    reader.ReadInt32(), reader.ReadInt32(), (LogType)reader.ReadInt32());
            return logs;
        }
    }

    private TextObject ReadText(BinaryReader reader)
    {
        var data = reader.ReadBytes(reader.ReadInt32());
        return data.Length == 0 ? null : BinaryPackageSerializer.Deserialize<TextObjectBinaryPackage>(data).Unpack<TextObject>(packages);
    }
}
