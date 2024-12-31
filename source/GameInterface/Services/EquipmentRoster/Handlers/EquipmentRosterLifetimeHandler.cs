using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.EquipmentRoster.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.Core;

namespace GameInterface.Services.EquipmentRoster.Handlers
{
    internal class EquipmentRosterLifetimeHandler : IHandler
    {
        private static readonly ILogger Logger = LogManager.GetLogger<EquipmentRosterLifetimeHandler>();
        private readonly IMessageBroker messageBroker;
        private readonly IObjectManager objectManager;
        private readonly INetwork network;

        public EquipmentRosterLifetimeHandler(IMessageBroker messageBroker, IObjectManager objectManager, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.objectManager = objectManager;
            this.network = network;
            messageBroker.Subscribe<EquipmentRosterCreated>(Handle);
            messageBroker.Subscribe<NetworkCreateEquipmentRoster>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<EquipmentRosterCreated>(Handle);
            messageBroker.Unsubscribe<NetworkCreateEquipmentRoster>(Handle);
        }

        private void Handle(MessagePayload<EquipmentRosterCreated> obj)
        {
            var payload = obj.What;

            if (objectManager.AddNewObject(payload.EquipmentRoster, out string EquipmentRosterId) == false) return;

            var message = new NetworkCreateEquipmentRoster(EquipmentRosterId);
            network.SendAll(message);
        }

        private void Handle(MessagePayload<NetworkCreateEquipmentRoster> obj)
        {
            var payload = obj.What;

            var EquipmentRoster = ObjectHelper.SkipConstructor<MBEquipmentRoster>();
            if (objectManager.AddExisting(payload.EquipmentRosterId, EquipmentRoster) == false)
            {
                Logger.Error("Failed to add existing EquipmentRoster, {id}", payload.EquipmentRosterId);
                return;
            }
        }
    }
}