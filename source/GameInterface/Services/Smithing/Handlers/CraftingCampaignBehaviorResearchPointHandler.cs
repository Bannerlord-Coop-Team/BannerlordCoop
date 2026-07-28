using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Smithing.Interfaces;
using GameInterface.Services.Smithing.Messages;
using Serilog;

namespace GameInterface.Services.Smithing.Handlers;

internal class CraftingCampaignBehaviorResearchPointHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<CraftingCampaignBehaviorResearchPointHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly ISessionCraftingPlayerDataInterface sessionCraftingPlayerDataInterface;

    public CraftingCampaignBehaviorResearchPointHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        ISessionCraftingPlayerDataInterface sessionCraftingPlayerDataInterface)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.sessionCraftingPlayerDataInterface = sessionCraftingPlayerDataInterface;

        messageBroker.Subscribe<UpdateResearchPoints>(Handle_UpdateResearchPoints);
        messageBroker.Subscribe<NetworkUpdateResearchPoints>(Handle_NetworkUpdateResearchPoints);

        messageBroker.Subscribe<OpenCraftingPart>(Handle_OpenCraftingPart);
        messageBroker.Subscribe<NetworkOpenCraftingPart>(Handle_NetworkOpenCraftingPart);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<UpdateResearchPoints>(Handle_UpdateResearchPoints);
        messageBroker.Subscribe<NetworkUpdateResearchPoints>(Handle_NetworkUpdateResearchPoints);

        messageBroker.Unsubscribe<OpenCraftingPart>(Handle_OpenCraftingPart);
        messageBroker.Subscribe<NetworkOpenCraftingPart>(Handle_NetworkOpenCraftingPart);
    }

    private void Handle_UpdateResearchPoints(MessagePayload<UpdateResearchPoints> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.MainHero, out string playerHeroId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.What.CraftingTemplate, out string craftingTemplateId)) return;

        network.SendAll(new NetworkUpdateResearchPoints(playerHeroId, craftingTemplateId, obj.What.NewXp));
    }

    private void Handle_NetworkUpdateResearchPoints(MessagePayload<NetworkUpdateResearchPoints> obj)
    {
        sessionCraftingPlayerDataInterface.SetCraftingPieceXp(
            obj.What.PlayerHeroId,
            obj.What.CraftingTemplateId,
            obj.What.NewXp);
    }

    private void Handle_OpenCraftingPart(MessagePayload<OpenCraftingPart> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.MainHero, out string playerHeroId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.What.CraftingTemplate, out string craftingTemplateId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.What.CraftingPiece, out string craftingPieceId)) return;

        network.SendAll(new NetworkOpenCraftingPart(playerHeroId, craftingTemplateId, craftingPieceId));
    }

    private void Handle_NetworkOpenCraftingPart(MessagePayload<NetworkOpenCraftingPart> obj)
    {
        sessionCraftingPlayerDataInterface.UnlockCraftingPiece(
            obj.What.PlayerHeroId,
            obj.What.CraftingTemplateId,
            obj.What.CraftingPieceId);
    }
}
