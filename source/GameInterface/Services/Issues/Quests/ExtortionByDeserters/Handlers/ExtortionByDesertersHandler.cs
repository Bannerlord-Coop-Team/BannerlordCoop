using Common;
using Common.Messaging;
using GameInterface.Services.Entity;
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
internal class ExtortionByDesertersHandler : IHandler
{
    private readonly IMessageBroker broker;
    private readonly IObjectManager objects;
    private readonly IIssueResolver issues;
    private readonly IControllerIdProvider controller;
    private readonly IIssueConversationGuard conversationGuard;
    private readonly IPlayerEncounterInterface playerEncounter;

    public ExtortionByDesertersHandler(IMessageBroker broker, IObjectManager objects,
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
            context: nameof(ExtortionByDesertersHandler));
    }

    private void Apply(NetworkExtortionByDesertersState data)
    {
        if (!issues.TryResolve(data.IssueOwnerId, data.IssueId, out _, out var issue, out _) ||
            !(issue.IssueQuest is Quest quest))
        {
            return;
        }

        if (!TryResolveParties(data, out var deserters, out var defenders)) return;

        BindParties(quest, deserters, defenders, (State)data.State);
        if (!TextObject.IsNullOrEmpty(data.Log)) quest.AddLog(data.Log);
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
            case ExtortionByDesertersNotice.DesertersDefeated:
                MBInformationManager.AddQuickInformation(quest.OnPlayerDefeatedDesertersNotificationText, 0, quest.QuestGiver.CharacterObject);
                break;
        }
    }
}
