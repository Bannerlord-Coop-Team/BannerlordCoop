using Common.Messaging;
using GameInterface.Extentions;
using GameInterface.Services.Entity;
using GameInterface.Services.MobileParties.Data;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.MobileParties.Patches;
using GameInterface.Services.ObjectManager;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;

namespace GameInterface.Services.MobileParties.Handlers
{
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
        private readonly IMessageBroker messageBroker;
        private readonly IControlledEntityRegistry controlledEntityRegistry;
        private readonly IObjectManager objectManager;
        private readonly IControllerIdProvider controllerIdProvider;

        public MobilePartyBehaviorHandler(
            IMessageBroker messageBroker, 
            IControlledEntityRegistry controlledEntityRegistry,
            IObjectManager objectManager,
            IControllerIdProvider controllerIdProvider) 
        {
            this.messageBroker = messageBroker;
            this.controlledEntityRegistry = controlledEntityRegistry;
            this.objectManager = objectManager;
            this.controllerIdProvider = controllerIdProvider;
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
            MobileParty party = obj.What.Party;

            if (controlledEntityRegistry.IsControlledBy(controllerIdProvider.ControllerId, party.StringId) == false)
                return;

            PartyBehaviorUpdateData data = obj.What.BehaviorUpdateData;

            messageBroker.Publish(this, new ControlledPartyBehaviorUpdated(data));
        }

        public void Handle_UpdatePartyBehavior(MessagePayload<UpdatePartyBehavior> obj)
        {
            var data = obj.What.BehaviorUpdateData;
            IMapEntity targetMapEntity = null;

            if (data.HasTarget && !objectManager.TryGetObject(data.TargetId, out targetMapEntity)) 
                return;

            if (!objectManager.TryGetObject(data.PartyId, out MobileParty party)) 
                return;

            Vec2 targetPoint = new Vec2(data.TargetPointX, data.TargetPointY);

            PartyBehaviorPatch.SetAiBehavior(
                party.Ai,
                data.Behavior,
                targetMapEntity,
                targetPoint
            );
        }
    }
}
