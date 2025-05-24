using Common.Messaging;
using Common.Network;
using GameInterface.Services.ObjectManager;
using System;
using System.Collections.Generic;
using System.Text;

namespace GameInterface.DynamicSync;

public class DynamicHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private List<IHandler> handlers = new List<IHandler>();

    public DynamicHandler(IMessageBroker messageBroker, IObjectManager objectManager, INetwork network)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
    }

    public void RegisterHandler(Type handlerType)
    {
       handlers.Add((IHandler)Activator.CreateInstance(handlerType, new object[] { messageBroker, objectManager, network }));
    }

    public void Dispose()
    {
        handlers.ForEach(h => h.Dispose());
    }
}
