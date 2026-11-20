using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Framework.Finalization;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Issues.Quests.ExtortionByDeserters.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.MapEvents.Interfaces;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using Quest = TaleWorlds.CampaignSystem.Issues.ExtortionByDesertersIssueBehavior.ExtortionByDesertersIssueQuest;
using State = TaleWorlds.CampaignSystem.Issues.ExtortionByDesertersIssueBehavior.ExtortionByDesertersIssueQuest.ExtortionByDesertersQuestState;

namespace GameInterface.Services.Issues.Quests.ExtortionByDeserters.Handlers;

/// <summary>Connects personal quest state to the server's existing world parties.</summary>
internal class ExtortionByDesertersStateHandler : IHandler
{
    private enum ClientNotice { None, DesertersDefeated }

    private readonly IMessageBroker broker;
    private readonly IObjectManager objects;
    private readonly IIssueResolver issues;
    private readonly IControllerIdProvider controller;
    private readonly IIssueConversationGuard conversationGuard;
    private readonly IPlayerEncounterInterface playerEncounter;

    public ExtortionByDesertersStateHandler(IMessageBroker broker, IObjectManager objects,
        IIssueResolver issues, IControllerIdProvider controller,
        IIssueConversationGuard conversationGuard, IPlayerEncounterInterface playerEncounter)
    {
        this.broker = broker;
        this.objects = objects;
        this.issues = issues;
        this.controller = controller;
        this.conversationGuard = conversationGuard;
        this.playerEncounter = playerEncounter;
        broker.Subscribe<NetworkExtortionByDesertersState>(Handle);
    }

    public void Dispose() => broker.Unsubscribe<NetworkExtortionByDesertersState>(Handle);

    private void Handle(MessagePayload<NetworkExtortionByDesertersState> payload)
    {
        var data = payload.What;
        if (ModInformation.IsServer || data.ControllerId != controller.ControllerId)
        {
            return;
        }

        GameThread.RunSafe(() => conversationGuard.RunInOrder(() => Apply(data), data.IssueOwnerId),
            context: nameof(ExtortionByDesertersStateHandler));
    }

    private void Apply(NetworkExtortionByDesertersState data)
    {
        if (!issues.TryResolve(data.IssueOwnerId, data.IssueId, out _, out var issue, out _) ||
            !(issue.IssueQuest is Quest quest))
        {
            return;
        }

        if (!TryResolveParties(data, out var deserters, out var defenders)) return;

        var state = (State)data.State;
        // Compare the old state before BindParties replaces it.
        var clientNotice = quest._currentState != State.DesertersAreDefeated && state == State.DesertersAreDefeated
            ? ClientNotice.DesertersDefeated
            : ClientNotice.None;
        BindParties(quest, deserters, defenders, state);
        if (!TextObject.IsNullOrEmpty(data.Log))
        {
            using var replay = new AllowedThread();
            using var journalReplay = new QuestJournalReplay();
            quest.AddLog(data.Log);
        }

        if (clientNotice == ClientNotice.DesertersDefeated)
        {
            MBInformationManager.AddQuickInformation(quest.OnPlayerDefeatedDesertersNotificationText, 0, quest.QuestGiver.CharacterObject);
            return;
        }

        var notice = (ExtortionByDesertersNotice)data.Notice;
        ShowNotice(quest, notice);
        if (notice == ExtortionByDesertersNotice.Ambush &&
            deserters != null && defenders != null && quest.IsOngoing)
        {
            playerEncounter.OpenEncounterMeeting(deserters.MapEvent, deserters.Party);
        }
    }

    private bool TryResolveParties(NetworkExtortionByDesertersState data, out MobileParty deserters, out MobileParty defenders)
    {
        deserters = null;
        defenders = null;
        if (!string.IsNullOrEmpty(data.DeserterId) && !objects.TryGetObjectWithLogging(data.DeserterId, out deserters))
            return false;
        if (!string.IsNullOrEmpty(data.DefenderId) && !objects.TryGetObjectWithLogging(data.DefenderId, out defenders))
            return false;

        return true;
    }

    private static void BindParties(Quest quest, MobileParty deserters, MobileParty defenders, State state)
    {
        if (quest._deserterMobileParty != null && quest._deserterMobileParty != deserters &&
            quest.IsTracked(quest._deserterMobileParty))
        {
            quest.RemoveTrackedObject(quest._deserterMobileParty);
        }
        if (deserters != null && quest._deserterMobileParty != deserters) quest.AddTrackedObject(deserters);
        quest._deserterMobileParty = deserters;
        quest._defenderMobileParty = defenders;
        quest._currentState = state;
        if (state == State.DesertersAreDefeated)
        {
            if (!quest.IsTracked(quest.QuestGiver)) quest.AddTrackedObject(quest.QuestGiver);
            if (!quest.IsTracked(quest.QuestSettlement)) quest.AddTrackedObject(quest.QuestSettlement);
        }
    }

    private static void ShowNotice(Quest quest, ExtortionByDesertersNotice notice)
    {
        switch (notice)
        {
            case ExtortionByDesertersNotice.DesertersNoticedPlayer:
                MBInformationManager.AddQuickInformation(quest.OnDesertersNoticedPlayerNotificationText, 0, Hero.MainHero.CharacterObject);
                break;
            case ExtortionByDesertersNotice.PlayerLeftVillage:
                MBInformationManager.AddQuickInformation(quest.OnPlayerLeftQuestSettlementNotificationText, 0, quest.QuestGiver.CharacterObject);
                break;
        }
    }
}
