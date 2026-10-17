using Common;
using Common.Messaging;
using GameInterface.Services.Entity;
using GameInterface.Services.Bandits.Messages;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using System;
using TaleWorlds.Core;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameState;
using Helpers;
using Quest = TaleWorlds.CampaignSystem.Issues.CaravanAmbushIssueBehavior.CaravanAmbushIssueQuest;

namespace GameInterface.Services.Issues.Quests.CaravanAmbush;

internal class CaravanAmbushStateHandler : IHandler
{
    private readonly IMessageBroker broker;
    private readonly IObjectManager objects;
    private readonly IIssueResolver issues;
    private readonly IControllerIdProvider controller;
    private readonly ICaravanAmbushEvents events;
    private NetworkCaravanAmbushConversation pendingConversation;

    public CaravanAmbushStateHandler(IMessageBroker broker, IObjectManager objects, IIssueResolver issues,
        IControllerIdProvider controller, ICaravanAmbushEvents events)
    {
        this.broker = broker;
        this.objects = objects;
        this.issues = issues;
        this.controller = controller;
        this.events = events;
        broker.Subscribe<NetworkCaravanAmbushState>(Handle);
        broker.Subscribe<NetworkCaravanAmbushConversation>(Conversation);
        broker.Subscribe<BanditPartyRecruitedByPlayer>(Recruited);
    }

    public void Dispose()
    {
        broker.Unsubscribe<NetworkCaravanAmbushState>(Handle);
        broker.Unsubscribe<NetworkCaravanAmbushConversation>(Conversation);
        broker.Unsubscribe<BanditPartyRecruitedByPlayer>(Recruited);
        ClearPendingConversation();
    }

    private void Conversation(MessagePayload<NetworkCaravanAmbushConversation> payload)
    {
        if (ModInformation.IsServer || payload.What.ControllerId != controller.ControllerId) return;
        GameThread.RunSafe(() =>
        {
            if (pendingConversation == null)
                CampaignEvents.TickEvent.AddNonSerializedListener(this, _ => TryOpenConversation());
            pendingConversation = payload.What;
            TryOpenConversation();
        }, context: nameof(CaravanAmbushStateHandler));
    }

    private void TryOpenConversation()
    {
        GameThread.RunSafe(() =>
        {
            var data = pendingConversation;
            if (data == null) return;
            if (data.ControllerId != controller.ControllerId ||
                !issues.TryResolve(data.IssueOwnerId, data.IssueId, out _, out var issue, out _) ||
                !(issue.IssueQuest is Quest quest) || !quest.IsOngoing || !quest._isCaravanSaved)
            {
                ClearPendingConversation();
                return;
            }
            // Battle results can arrive before the owner leaves the mission and loot screens.
            if (!(GameStateManager.Current?.ActiveState is MapState map) || map.AtMenu || map.IsSimulationActive ||
                Campaign.Current.ConversationManager.IsConversationInProgress) return;
            if (!objects.TryGetObjectWithLogging<MobileParty>(data.CaravanId, out var caravan) || !caravan.IsActive)
            {
                ClearPendingConversation();
                return;
            }
            var leader = ConversationHelper.GetConversationCharacterPartyLeader(caravan.Party);
            if (leader == null) return;
            CampaignMapConversation.OpenConversation(
                new ConversationCharacterData(Hero.MainHero.CharacterObject),
                new ConversationCharacterData(leader));
            ClearPendingConversation();
        }, context: nameof(CaravanAmbushStateHandler));
    }

    private void ClearPendingConversation()
    {
        pendingConversation = null;
        if (Campaign.Current != null) CampaignEvents.TickEvent.ClearListeners(this);
    }

    private void Recruited(MessagePayload<BanditPartyRecruitedByPlayer> payload)
    {
        if (ModInformation.IsClient) return;
        var data = payload.What;
        events.Run(quest =>
        {
            if (quest._banditParty != data.Bandits) return;
            if (MobileParty.MainParty == data.Recruiter) quest.OnPlayerHiredBandits();
            else quest.CompleteQuestWithCancel();
        });
    }

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
            if (data.Warnings > quest._vicinityCheckFailCounter && data.Warnings < 4)
            {
                var warning = data.Warnings == 3
                    ? new TextObject("{=uD2pfRAh}Get back immediately! If you keep this close to the caravan the ambushers will certainly notice you.")
                    : new TextObject("{=ki1CWgcP}Warning! You are too close to the caravan. Stay a bit farther away.");
                MBInformationManager.AddQuickInformation(warning);
            }
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
