using ProtoBuf;
using GameInterface.Helpers;
using GameInterface;
using Common.Messaging;

namespace Coop.Mod.EventHandlers
{
    internal class ExampleHandler : EventHandlerBase
    {
        private IExampleGameHelper _exampleHelper;
        public ExampleHandler(IMessageBroker messageBroker, IGameInterface gameInterface) : base(messageBroker, gameInterface)
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
