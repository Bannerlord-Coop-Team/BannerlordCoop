using Common;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Armies.Messages;
using GameInterface.Services.ObjectManager;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Armies.Handlers
{
    public class ArmyCohesionHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly IObjectManager objectManager;
        private readonly INetwork network;

        public ArmyCohesionHandler(IMessageBroker messageBroker, IObjectManager objectManager, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.objectManager = objectManager;
            this.network = network;

            messageBroker.Subscribe<ArmyCohesionChanged>(Handle);
            messageBroker.Subscribe<NetworkArmyCohesionChanged>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<ArmyCohesionChanged>(Handle);
            messageBroker.Unsubscribe<NetworkArmyCohesionChanged>(Handle);
        }

        private void Handle(MessagePayload<ArmyCohesionChanged> payload)
        {
            var obj = payload.What;
            if (!objectManager.TryGetIdWithLogging(obj.Army, out var armyId)) return;
            network.SendAll(new NetworkArmyCohesionChanged(armyId, obj.Cohesion));
        }
        private void Handle(MessagePayload<NetworkArmyCohesionChanged> payload)
        {
            var obj = payload.What;
            GameThread.RunSafe(() =>
            {
                if (!objectManager.TryGetObjectWithLogging<Army>(obj.ArmyId, out var army))
                    return;

                using (new AllowedThread())
                {
                    army.Cohesion = obj.Cohesion;
                }
            });
        }
    }
}
