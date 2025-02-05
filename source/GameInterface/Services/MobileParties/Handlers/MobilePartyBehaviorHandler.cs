using Common.Logging;
using Common.Messaging;
using GameInterface.Services.Entity;
using GameInterface.Services.MobileParties.Data;
using GameInterface.Services.MobileParties.Interfaces;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.MobilePartyAIs.Patches;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.MobileParties.Handlers;

/// <summary>
/// Handles synchronization of the <see cref="MobilePartyAi"/>'s behavior on the campaign map, which includes
/// target positions and target entities used for updating movement.
/// </summary>
/// <remarks>
/// Important note: <see cref="MobilePartyAi"/> is also present in player-controlled parties, where it is 
/// responsible for pathfinding and movement.
/// </remarks>
/// <seealso cref="AiBehavior"/>
internal class MobilePartyBehaviorHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<MobilePartyBehaviorHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IControlledEntityRegistry controlledEntityRegistry;
    private readonly IControllerIdProvider controllerIdProvider;
    private readonly IMobilePartyInterface mobilePartyInterface;
    private readonly IObjectManager objectManager;

    public MobilePartyBehaviorHandler(
        IMessageBroker messageBroker,
        IControlledEntityRegistry controlledEntityRegistry,
        IControllerIdProvider controllerIdProvider,
        IMobilePartyInterface mobilePartyInterface,
        IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.controlledEntityRegistry = controlledEntityRegistry;
        this.controllerIdProvider = controllerIdProvider;
        this.mobilePartyInterface = mobilePartyInterface;
        this.objectManager = objectManager;

        messageBroker.Subscribe<PartyBehaviorChangeAttempted>(Handle_PartyBehaviorChanged);
        messageBroker.Subscribe<UpdatePartyBehavior>(Handle_UpdatePartyBehavior);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<PartyBehaviorChangeAttempted>(Handle_PartyBehaviorChanged);
        messageBroker.Unsubscribe<UpdatePartyBehavior>(Handle_UpdatePartyBehavior);
    }

    public void Handle_PartyBehaviorChanged(MessagePayload<PartyBehaviorChangeAttempted> obj)
    {
        var party = obj.What.Party;

        var controllerId = controllerIdProvider.ControllerId;

        if (controlledEntityRegistry.IsControlledBy(controllerId, party.StringId) == false)
            return;

        PartyBehaviorUpdateData data = obj.What.BehaviorUpdateData;

        messageBroker.Publish(this, new ControlledPartyBehaviorUpdated(data));
    }

    public void Handle_UpdatePartyBehavior(MessagePayload<UpdatePartyBehavior> obj)
    {
        var data = obj.What.BehaviorUpdateData;

        MobileParty targetParty = null;
        Settlement targetSettlement = null;
        if (data.HasTarget && 
            !objectManager.TryGetObject(data.TargetId, out targetParty) &&
            !objectManager.TryGetObject(data.TargetId, out targetSettlement))
            return;

        if (!objectManager.TryGetObject(data.PartyId, out MobileParty party))
            return;

        if (party.Ai == null) return;

        Vec2 targetPoint = new Vec2(data.TargetPointX, data.TargetPointY);

        IMapEntity targetMapEntity = null;
        if (data.HasTarget && targetParty != null)
        {
            targetMapEntity = targetParty;
        }
        else if (data.HasTarget && targetSettlement != null)
        {
            targetMapEntity = targetSettlement;
        }

        PartyBehaviorPatch.SetAiBehavior(
            party.Ai,
            data.Behavior,
            targetMapEntity,
            targetPoint
        );

        if (ModInformation.IsClient)
        {
            party.Position2D = new Vec2(data.PartyPositionX, data.PartyPositionY);
        }
        else
        {
            data.PartyPositionX = party.Position2D.x;
            data.PartyPositionY = party.Position2D.y;
            messageBroker.Publish(this, new PartyBehaviorUpdated(ref data));
        }
    }
}