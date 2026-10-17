using Common;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;
using Quest = TaleWorlds.CampaignSystem.Issues.ExtortionByDesertersIssueBehavior.ExtortionByDesertersIssueQuest;
using State = TaleWorlds.CampaignSystem.Issues.ExtortionByDesertersIssueBehavior.ExtortionByDesertersIssueQuest.ExtortionByDesertersQuestState;

namespace GameInterface.Services.Issues.Quests.ExtortionByDeserters;

/// <summary>Connects personal quest state to the server's existing world parties.</summary>
internal class ExtortionByDesertersSync : IHandler
{
    private readonly IMessageBroker broker;
    private readonly INetwork network;
    private readonly IObjectManager objects;
    private readonly IIssueResolver issues;
    private readonly IIssueOwnerResolver owners;
    private readonly IControllerIdProvider controller;

    public ExtortionByDesertersSync(IMessageBroker broker, INetwork network, IObjectManager objects,
        IIssueResolver issues, IIssueOwnerResolver owners, IControllerIdProvider controller)
    {
        this.broker = broker;
        this.network = network;
        this.objects = objects;
        this.issues = issues;
        this.owners = owners;
        this.controller = controller;
        broker.Subscribe<NetworkExtortionByDesertersState>(Handle);
    }

    public void Dispose() => broker.Unsubscribe<NetworkExtortionByDesertersState>(Handle);

    public void Send(Quest quest, string log = "", bool ambush = false)
    {
        if (ModInformation.IsClient)
        {
            return;
        }

        var issue = IssueManager.GetIssueOfQuest(quest);
        if (issue == null || !objects.TryGetIdWithLogging(issue.IssueOwner, out var ownerId) ||
            !owners.TryGetOwnerControllerId(issue, out var controllerId))
        {
            return;
        }

        var deserterId = string.Empty;
        var defenderId = string.Empty;
        if (quest._deserterMobileParty != null && !objects.TryGetIdWithLogging(quest._deserterMobileParty, out deserterId)) return;
        if (quest._defenderMobileParty != null && !objects.TryGetIdWithLogging(quest._defenderMobileParty, out defenderId)) return;

        network.SendAll(new NetworkExtortionByDesertersState(ownerId, issue.StringId, controllerId,
            deserterId, defenderId, (int)quest._currentState, log, ambush));
    }

    private void Handle(MessagePayload<NetworkExtortionByDesertersState> payload)
    {
        var data = payload.What;
        if (ModInformation.IsServer || data.ControllerId != controller.ControllerId)
        {
            return;
        }

        GameThread.RunSafe(() =>
        {
            if (!issues.TryResolve(data.IssueOwnerId, data.IssueId, out _, out var issue, out _) ||
                !(issue.IssueQuest is Quest quest)) return;

            MobileParty deserters = null;
            MobileParty defenders = null;
            if (data.DeserterId.Length != 0 && !objects.TryGetObjectWithLogging(data.DeserterId, out deserters)) return;
            if (data.DefenderId.Length != 0 && !objects.TryGetObjectWithLogging(data.DefenderId, out defenders)) return;

            if (deserters != null && quest._deserterMobileParty != deserters)
            {
                quest.AddTrackedObject(deserters);
            }

            quest._deserterMobileParty = deserters;
            quest._defenderMobileParty = defenders;
            quest._currentState = (State)data.State;
            if (data.Log.Length != 0) quest.AddLog(new TextObject(data.Log));
            if (data.Ambush && deserters != null && defenders != null && quest.IsOngoing)
            {
                quest.StartAmbushEncounter();
            }
        }, context: nameof(ExtortionByDesertersSync));
    }
}
