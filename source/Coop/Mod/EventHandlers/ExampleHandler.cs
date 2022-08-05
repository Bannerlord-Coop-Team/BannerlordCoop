using Common.MessageBroker;
using Coop.Mod.GameInterfaces.Helpers;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coop.Mod.EventHandlers
{
    internal class ExampleHandler : EventHandlerBase
    {
        private IExampleGameHelper _exampleHelper;
        public ExampleHandler(ICommunicator communicator) : base(communicator)
        {
            _exampleHelper = _gameInterface.ExampleGameHelper;

            _messageBroker.Subscribe<ExampleIncomingMessage>(Handle_ExampleHandler);
        }

        private void Handle_ExampleHandler(MessagePayload<ExampleIncomingMessage> payload)
        {
            // Construct new message with data
            ExampleOutgoingMessage newMessage = new ExampleOutgoingMessage(payload.What.ExampleData);

            // Do some game behavior with the game interface component
            _exampleHelper.GoToMainMenu();

            // Send internally
            _messageBroker.Publish(this, newMessage);

            // Send externally (server to client if this handler exists in the server)
            //                 (client to server if this handler exists in the client)
            // Message is required to be a protocontract on external messages
            _messageBroker.Publish(this, newMessage, MessageScope.External);
        }
    }

    public readonly struct ExampleIncomingMessage
    {
        public int ExampleData { get; }

        public ExampleIncomingMessage(int exampleData)
        {
            ExampleData = exampleData;
        }
    }

    [ProtoContract]
    public readonly struct ExampleOutgoingMessage
    {
        [ProtoMember(1)]
        public int ExampleData { get; }

        public ExampleOutgoingMessage(int exampleData)
        {
            ExampleData = exampleData;
        }
    }
}
