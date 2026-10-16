using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Network.Messages;
using GameInterface.Services.Education.Interfaces;
using GameInterface.Services.Education.Messages;
using GameInterface.Services.GameState.Messages;
using GameInterface.Services.Heroes.HeirSelection.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Messages;
using LiteNetLib;
using Serilog;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Services.Education.Handlers;

internal class EducationCampaignBehaviorHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<EducationCampaignBehaviorHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly IPlayerManager playerManager;
    private readonly IEducationCampaignBehaviorInterface educationInterface;

    public EducationCampaignBehaviorHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        IPlayerManager playerManager,
        IEducationCampaignBehaviorInterface educationInterface)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.playerManager = playerManager;
        this.educationInterface = educationInterface;

        messageBroker.Subscribe<CampaignReady>(Handle_CampaignReady);

        messageBroker.Subscribe<SendEducationNotification>(Handle_SendEducationNotification);
        messageBroker.Subscribe<NetworkSendEducationNotification>(Handle_NetworkSendEducationNotification);

        messageBroker.Subscribe<CancelEducationNotification>(Handle_CancelEducationNotification);
        messageBroker.Subscribe<NetworkCancelEducationNotification>(Handle_NetworkCancelEducationNotification);

        messageBroker.Subscribe<EducationStageUpdated>(Handle_EducationStageUpdated);
        messageBroker.Subscribe<NetworkEducationStageUpdated>(Handle_NetworkEducationStageUpdated);
        messageBroker.Subscribe<NetworkEducationStageRejected>(Handle_NetworkEducationStageRejected);

        messageBroker.Subscribe<PlayerDisconnected>(Handle_PlayerDisconnected);
        messageBroker.Subscribe<PlayerDeletionStarted>(Handle_PlayerDeletionStarted);
        messageBroker.Subscribe<PlayerCharacterChangedAfterHeirSelection>(Handle_PlayerCharacterChangedAfterHeirSelection);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<CampaignReady>(Handle_CampaignReady);

        messageBroker.Unsubscribe<SendEducationNotification>(Handle_SendEducationNotification);
        messageBroker.Unsubscribe<NetworkSendEducationNotification>(Handle_NetworkSendEducationNotification);

        messageBroker.Unsubscribe<CancelEducationNotification>(Handle_CancelEducationNotification);
        messageBroker.Unsubscribe<NetworkCancelEducationNotification>(Handle_NetworkCancelEducationNotification);

        messageBroker.Unsubscribe<EducationStageUpdated>(Handle_EducationStageUpdated);
        messageBroker.Unsubscribe<NetworkEducationStageUpdated>(Handle_NetworkEducationStageUpdated);
        messageBroker.Unsubscribe<NetworkEducationStageRejected>(Handle_NetworkEducationStageRejected);

        messageBroker.Unsubscribe<PlayerDisconnected>(Handle_PlayerDisconnected);
        messageBroker.Unsubscribe<PlayerDeletionStarted>(Handle_PlayerDeletionStarted);
        messageBroker.Unsubscribe<PlayerCharacterChangedAfterHeirSelection>(Handle_PlayerCharacterChangedAfterHeirSelection);
    }

    private void Handle_CampaignReady(MessagePayload<CampaignReady> obj)
    {
        if (ModInformation.IsServer) return;

        // Drop _previousEducations loaded from the join save, the server owns education progress
        educationInterface.ClearPreviousEducations();
    }

    private void Handle_SendEducationNotification(MessagePayload<SendEducationNotification> obj)
    {
        var data = obj.What;

        if (!playerManager.TryGetPeer(data.ControllerId, out var peer)) return;
        if (!objectManager.TryGetHandleWithLogging(data.ChildToEducate, out var childToEducateHandle)) return;

        var message = new NetworkSendEducationNotification(childToEducateHandle, data.Age, data.Education);
        network.Send(peer, message);
    }

    private void Handle_NetworkSendEducationNotification(MessagePayload<NetworkSendEducationNotification> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.ChildToEducateHandle, out var childToEducate)) return;

            educationInterface.ShowEducationNotification(childToEducate, data.Age, data.Education);
        });
    }

    private void Handle_CancelEducationNotification(MessagePayload<CancelEducationNotification> obj)
    {
        var data = obj.What;

        if (!playerManager.TryGetPeer(data.ControllerId, out var peer)) return;
        if (!objectManager.TryGetHandleWithLogging(data.ChildToEducate, out var childToEducateHandle)) return;

        network.Send(peer, new NetworkCancelEducationNotification(childToEducateHandle, data.Age));
    }

    private void Handle_NetworkCancelEducationNotification(MessagePayload<NetworkCancelEducationNotification> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.ChildToEducateHandle, out var childToEducate)) return;

            educationInterface.RemoveEducationNotification(childToEducate, data.Age);
        });
    }

    private void Handle_EducationStageUpdated(MessagePayload<EducationStageUpdated> obj)
    {
        var data = obj.What;

        if (!objectManager.TryGetHandleWithLogging(data.Child, out var childHandle)) return;

        var attributeIds = new List<string>();
        foreach (var attribute in data.Attributes)
        {
            if (objectManager.TryGetIdWithLogging(attribute, out var attributeId)) continue;

            attributeIds.Add(attributeId);
        }

        var skillIds = new List<string>();
        foreach (var skill in data.Skills)
        {
            if (objectManager.TryGetIdWithLogging(skill, out var skillId)) continue;

            skillIds.Add(skillId);
        }

        network.SendAll(new NetworkEducationStageUpdated(childHandle, data.Target, attributeIds, skillIds));
    }

    private void Handle_NetworkEducationStageUpdated(MessagePayload<NetworkEducationStageUpdated> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.ChildHandle, out var child)) return;

            var attributes = new List<CharacterAttribute>();
            foreach (var attributeId in data.AttributeIds ?? new List<string>())
            {
                if (!objectManager.TryGetObjectWithLogging<CharacterAttribute>(attributeId, out var attribute)) continue;

                attributes.Add(attribute);
            }

            var skills = new List<SkillObject>();
            foreach (var skillId in data.SkillIds ?? new List<string>())
            {
                if (objectManager.TryGetObjectWithLogging<SkillObject>(skillId, out var skill)) continue;

                skills.Add(skill);
            }

            if (educationInterface.UpdateEducationStage(child, data.Target, attributes, skills)) return;

            network.Send((NetPeer)obj.Who, new NetworkEducationStageRejected(data.ChildHandle));
        });
    }

    private void Handle_NetworkEducationStageRejected(MessagePayload<NetworkEducationStageRejected> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.ChildHandle, out var child)) return;

            var text = GameTexts.FindText("str_coop_education_stage_rejected").SetTextVariable("CHILD", child.Name);
            InformationManager.DisplayMessage(new InformationMessage(text.ToString()));
        });
    }

    private void Handle_PlayerDisconnected(MessagePayload<PlayerDisconnected> obj)
    {
        if (ModInformation.IsClient) return;

        GameThread.RunSafe(() => educationInterface.RemoveDisconnectedPlayers());
    }

    private void Handle_PlayerDeletionStarted(MessagePayload<PlayerDeletionStarted> obj)
    {
        if (ModInformation.IsClient) return;

        // Resolve now, the deletion removes the player registration right after publishing this
        if (!playerManager.TryGetPlayer(obj.What.Peer, out var deletedPlayer)) return;

        educationInterface.CancelPendingNotifications((_, controllerId) => controllerId == deletedPlayer.ControllerId);
    }

    private void Handle_PlayerCharacterChangedAfterHeirSelection(MessagePayload<PlayerCharacterChangedAfterHeirSelection> obj)
    {
        if (ModInformation.IsClient) return;

        // The registration may already be moved to the heir
        if (!playerManager.TryGetPlayer(obj.What.OldPlayer, out var player)
            && !playerManager.TryGetPlayer(obj.What.NewPlayer, out player)) return;

        // The heir may not be a suitable educator, OnDailyTick picks again
        educationInterface.CancelPendingNotifications((_, controllerId) => controllerId == player.ControllerId);
    }
}
