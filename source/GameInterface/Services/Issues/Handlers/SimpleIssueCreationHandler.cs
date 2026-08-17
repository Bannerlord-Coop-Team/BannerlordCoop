using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Issues.Handlers;

internal class SimpleIssueCreationHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<SimpleIssueCreationHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly IIssueGenerationRegistry generationRegistry;

    public SimpleIssueCreationHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        IIssueGenerationRegistry generationRegistry)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.generationRegistry = generationRegistry;

        messageBroker.Subscribe<SimpleIssueCreated>(Handle_SimpleIssueCreated);
        messageBroker.Subscribe<NetworkSimpleIssueCreated>(Handle_NetworkSimpleIssueCreated);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<SimpleIssueCreated>(Handle_SimpleIssueCreated);
        messageBroker.Unsubscribe<NetworkSimpleIssueCreated>(Handle_NetworkSimpleIssueCreated);
    }

    private void Handle_SimpleIssueCreated(MessagePayload<SimpleIssueCreated> payload)
    {
        if (ModInformation.IsClient) return;

        var issue = payload.What.Issue;
        if (issue?.IssueOwner == null) return;
        if (!objectManager.TryGetIdWithLogging(issue.IssueOwner, out var ownerId)) return;
        if (!SimpleIssueFactoryRegistry.TryGetKey(issue, out var key))
        {
            Logger.Error("SimpleIssueCreated published for an unregistered issue type {Type} for owner {Owner}", issue.GetType(), ownerId);
            return;
        }

        var generation = generationRegistry.Bump(issue.IssueOwner);

        network.SendAll(new NetworkSimpleIssueCreated(ownerId, key, generation));
    }

    private void Handle_NetworkSimpleIssueCreated(MessagePayload<NetworkSimpleIssueCreated> payload)
    {
        if (ModInformation.IsServer) return;

        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.OwnerId, out var owner)) return;

            generationRegistry.SetGeneration(owner, data.Generation);

            if (owner.Issue != null) return;

            try
            {
                if (!SimpleIssueFactoryRegistry.TryConstructAndRegister(data.IssueKey, owner))
                {
                    Logger.Error("Received NetworkSimpleIssueCreated with unknown IssueKey {Key} for owner {Owner}", data.IssueKey, data.OwnerId);
                }
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to construct issue for {Message} with IssueKey {Key} for owner {Owner}",
                    nameof(NetworkSimpleIssueCreated), data.IssueKey, data.OwnerId);
            }
        });
    }
}
