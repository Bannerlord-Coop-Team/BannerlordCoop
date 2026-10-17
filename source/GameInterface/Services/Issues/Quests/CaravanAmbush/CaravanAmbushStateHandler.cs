using Common;
using Common.Messaging;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using System;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;
using Quest = TaleWorlds.CampaignSystem.Issues.CaravanAmbushIssueBehavior.CaravanAmbushIssueQuest;

namespace GameInterface.Services.Issues.Quests.CaravanAmbush;

internal class CaravanAmbushStateHandler : IHandler
{
    private readonly IMessageBroker broker;
    private readonly IObjectManager objects;
    private readonly IIssueResolver issues;
    private readonly IControllerIdProvider controller;

    public CaravanAmbushStateHandler(IMessageBroker broker, IObjectManager objects, IIssueResolver issues, IControllerIdProvider controller)
    {
        this.broker = broker;
        this.objects = objects;
        this.issues = issues;
        this.controller = controller;
        broker.Subscribe<NetworkCaravanAmbushState>(Handle);
    }

    public void Dispose() => broker.Unsubscribe<NetworkCaravanAmbushState>(Handle);

    private void Handle(MessagePayload<NetworkCaravanAmbushState> payload)
    {
        if (ModInformation.IsServer || payload.What.ControllerId != controller.ControllerId) return;
        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!issues.TryResolve(data.IssueOwnerId, data.IssueId, out _, out var issue, out _) ||
                !(issue.IssueQuest is Quest quest)) return;
            if (!TryResolveParty(data.CaravanId, out var caravan) || !TryResolveParty(data.BanditId, out var bandits)) return;
            quest._caravanParty = caravan;
            quest._banditParty = bandits;
            quest._isCaravanSaved = data.Saved;
            quest._vicinityCheckFailCounter = data.Warnings;
            foreach (var party in new[] { caravan, bandits })
            {
                if (party != null && !quest.IsTracked(party)) quest.AddTrackedObject(party);
            }
            var journal = data.Journal ?? Array.Empty<string>();
            for (var i = quest.JournalEntries.Count; i < journal.Length; i++)
            {
                quest.AddLog(new TextObject("{=!}" + journal[i]));
            }
        }, context: nameof(CaravanAmbushStateHandler));
    }

    private bool TryResolveParty(string id, out MobileParty party)
    {
        party = null;
        return string.IsNullOrEmpty(id) || objects.TryGetObjectWithLogging(id, out party);
    }
}
