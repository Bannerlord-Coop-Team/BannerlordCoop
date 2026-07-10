using Common.Messaging;
using Common.Network;
using Common.Network.Coalescing;
using GameInterface.Services.ObjectManager;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GameInterface.AutoSync
{
    public class AutoSyncHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly IObjectManager objectManager;
        private readonly INetwork network;
        private readonly AutoSyncRegistry autoSyncRegistry;
        private readonly ISendCoalescer sendCoalescer;
        private List<IHandler> handlers = new List<IHandler>();

        public AutoSyncHandler(
            IMessageBroker messageBroker,
            IObjectManager objectManager,
            INetwork network,
            AutoSyncRegistry autoSyncRegistry,
            ISendCoalescer sendCoalescer = null)
        {
            this.messageBroker = messageBroker;
            this.objectManager = objectManager;
            this.network = network;
            this.autoSyncRegistry = autoSyncRegistry;
            this.sendCoalescer = sendCoalescer;
        }

        public void RegisterHandler(Type handlerType)
        {
           if(!handlers.Any(h => h.GetType() == handlerType))
                handlers.Add((IHandler)Activator.CreateInstance(handlerType, new object[] { messageBroker, objectManager, network, autoSyncRegistry, sendCoalescer }));
        }

        public void Dispose()
        {
            handlers.ForEach(h => h.Dispose());
        }
    }
}
