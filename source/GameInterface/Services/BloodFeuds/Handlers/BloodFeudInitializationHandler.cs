using Common;
using Common.Messaging;
using GameInterface.Services.BloodFeuds.Data;
using GameInterface.Services.BloodFeuds.Interfaces;
using GameInterface.Services.BloodFeuds.Messages;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.ObjectManager;
using System.Collections.Generic;

namespace GameInterface.Services.BloodFeuds.Handlers;

/// <summary>
/// On a joining client, applies the player's blood feuds from the transferred CoopSession once that client's main hero is known
/// </summary>
internal class BloodFeudInitializationHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly IExecutionCampaignBehaviorInterface executionInterface;

    private BloodFeudPlayerData bloodFeudPlayerData;

    public BloodFeudInitializationHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        IExecutionCampaignBehaviorInterface executionInterface)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.executionInterface = executionInterface;

        messageBroker.Subscribe<InitializeClientBloodFeudData>(Handle_InitializeClientBloodFeudData);
        messageBroker.Subscribe<PlayerHeroChanged>(Handle_PlayerHeroChanged);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<InitializeClientBloodFeudData>(Handle_InitializeClientBloodFeudData);
        messageBroker.Unsubscribe<PlayerHeroChanged>(Handle_PlayerHeroChanged);
    }

    private void Handle_InitializeClientBloodFeudData(MessagePayload<InitializeClientBloodFeudData> payload)
    {
        bloodFeudPlayerData = payload.What.BloodFeudPlayerData;
    }

    private void Handle_PlayerHeroChanged(MessagePayload<PlayerHeroChanged> payload)
    {
        if (ModInformation.IsServer) return;

        var newHero = payload.What.NewHero;
        if (newHero == null) return;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetIdWithLogging(newHero, out var playerHeroId)) return;

            Dictionary<string, BloodFeudData> feuds = null;
            bloodFeudPlayerData?.PlayerBloodFeuds?.TryGetValue(playerHeroId, out feuds);
            executionInterface.ApplyBloodFeuds(feuds);
        });
    }
}
