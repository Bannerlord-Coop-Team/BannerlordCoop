using Common.Messages;
using GameInterface.Helpers;

namespace GameInterface
{
    public class GameInterface : IGameInterface
    {
        public static IMessageBroker MessageBroker { get; private set; }

        public IExampleGameHelper ExampleGameHelper { get; }

        public ISaveLoadHelper SaveLoadHelper { get; }

        public GameInterface(
            IMessageBroker messageBroker,
            IExampleGameHelper exampleGameHelper,
            ISaveLoadHelper saveLoadHelper)
        {
            MessageBroker = messageBroker;
            ExampleGameHelper = exampleGameHelper;
            SaveLoadHelper = saveLoadHelper;
        }
    }
}
