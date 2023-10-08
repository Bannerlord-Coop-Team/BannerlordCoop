using Common.Messaging;
using GameInterface.Extentions;
using GameInterface.Services.Entity;
using GameInterface.Services.Kingdoms.Messages;
using GameInterface.Services.Kingdoms.Patches;
using GameInterface.Services.MobileParties.Data;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.MobileParties.Patches;
using GameInterface.Services.ObjectManager;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;

namespace GameInterface.Services.Kingdoms.Handlers;

/// <summary>
/// Kingdom/Faction War Handler
/// </summary>
internal class FactionWarHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IControlledEntityRegistry controlledEntityRegistry;
    private readonly IControllerIdProvider controllerIdProvider;
    private readonly IObjectManager objectManager;

    public FactionWarHandler(
        IMessageBroker messageBroker,
        IControlledEntityRegistry controlledEntityRegistry,
        IControllerIdProvider controllerIdProvider,
        IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.controlledEntityRegistry = controlledEntityRegistry;
        this.controllerIdProvider = controllerIdProvider;
        this.objectManager = objectManager;

        messageBroker.Subscribe<WarDeclared>(Handle);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<WarDeclared>(Handle);
    }

    public void Handle(MessagePayload<WarDeclared> obj)
    {
        var payload = obj.What;

        Kingdom faction1 = Kingdom.All.Find(x => x.StringId == payload.Faction1Id);
        Kingdom faction2 = Kingdom.All.Find(x => x.StringId == payload.Faction2Id);
        DeclareWarAction.DeclareWarDetail detail = (DeclareWarAction.DeclareWarDetail)payload.Detail;

        DeclareWarActionPatch.RunOriginalApplyInternal(faction1, faction2, detail);
    }
}