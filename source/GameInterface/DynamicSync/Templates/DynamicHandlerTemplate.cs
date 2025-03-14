using Common.Messaging;
using Common.Network;
using GameInterface.Services.ObjectManager;
using GameInterface.Utils;
using System;
using System.Linq;
@UsingDeclarations@

namespace DynamicSync
{
    public class @HandlerType@ : GenericHandler<@ClassType@, @HandlerType@>
    {

        public @HandlerType@(IMessageBroker messageBroker, IObjectManager objectManager, INetwork network) : base(messageBroker, objectManager, network)
        {
            @Subscriptions@
        }
    }
}
