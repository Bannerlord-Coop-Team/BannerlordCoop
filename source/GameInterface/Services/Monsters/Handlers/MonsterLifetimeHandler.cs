using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Monsters.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.Core;

namespace GameInterface.Services.Monsters.Handlers
{
    internal class MonsterLifetimeHandler : IHandler
    {
        private static readonly ILogger Logger = LogManager.GetLogger<MonsterLifetimeHandler>();
        private readonly IMessageBroker messageBroker;
        private readonly IObjectManager objectManager;
        private readonly INetwork network;

        public MonsterLifetimeHandler(IMessageBroker messageBroker, IObjectManager objectManager, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.objectManager = objectManager;
            this.network = network;
            messageBroker.Subscribe<MonsterCreated>(Handle);
            messageBroker.Subscribe<NetworkCreateMonster>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<MonsterCreated>(Handle);
            messageBroker.Unsubscribe<NetworkCreateMonster>(Handle);
        }

        private void Handle(MessagePayload<MonsterCreated> obj)
        {
            var payload = obj.What;

            if (objectManager.AddNewObject(payload.Monster, out string monsterId) == false) return;

            var message = new NetworkCreateMonster(monsterId);
            network.SendAll(message);
        }

        private void Handle(MessagePayload<NetworkCreateMonster> obj)
        {
            var payload = obj.What;

            var monster = ObjectHelper.SkipConstructor<Monster>();
            if (objectManager.AddExisting(payload.MonsterId, monster) == false)
            {
                Logger.Error("Failed to add existing Building, {id}", payload.MonsterId);
                return;
            }
        }
    }
}
