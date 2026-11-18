using Common;
using Common.Messaging;
using GameInterface.AutoSync;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Issues.Quests.ExtortionByDeserters;
using GameInterface.Services.Issues.Quests.ExtortionByDeserters.Patches;
using GameInterface.Services.Issues.Quests.ExtortionByDeserters.Messages;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
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

    public ExtortionByDesertersHandler(IMessageBroker broker, IObjectManager objects,
        IIssueResolver issues, IControllerIdProvider controller,
        IAutoSyncPatchCollector patchCollector, IQuestFeatureFlag questFeatureFlag,
        IIssueConversationGuard conversationGuard)
    {
        if (questFeatureFlag.IsEnabled)
        {
            // Compile these callers after AutoSync so they cannot inline unpatched setters.
            var prefix = AccessTools.Method(typeof(ExtortionByDesertersServerMethodsPatch), nameof(ExtortionByDesertersServerMethodsPatch.Prefix));
            foreach (var method in ExtortionByDesertersServerMethodsPatch.TargetMethods()) patchCollector.AddPrefix(method, prefix);
        }

        this.broker = broker;
        this.objects = objects;
        this.issues = issues;
        this.controller = controller;
        this.conversationGuard = conversationGuard;
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
            !(issue.IssueQuest is Quest quest) || !TryResolveParties(data, out var deserters, out var defenders))
        {
            return;
        }

        BindParties(quest, deserters, defenders, (State)data.State);
        if (!TextObject.IsNullOrEmpty(data.Log)) quest.AddLog(data.Log);
        ShowNotice(quest, (ExtortionByDesertersNotice)data.Notice);
        if (data.Ambush && deserters != null && defenders != null && quest.IsOngoing) quest.StartAmbushEncounter();
    }

    private bool TryResolveParties(NetworkExtortionByDesertersState data, out MobileParty deserters, out MobileParty defenders)
    {
        deserters = null;
        defenders = null;
        return (string.IsNullOrEmpty(data.DeserterId) || objects.TryGetObjectWithLogging(data.DeserterId, out deserters)) &&
            (string.IsNullOrEmpty(data.DefenderId) || objects.TryGetObjectWithLogging(data.DefenderId, out defenders));
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
