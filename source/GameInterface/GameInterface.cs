using Autofac;
using Common.Messaging;
using GameInterface.Helpers;
using GameInterface.Serialization.DynamicModel;
using GameInterface.Services.GameState;

namespace GameInterface
{
    public class GameInterface : IGameInterface
    {
        public static IMessageBroker MessageBroker { get; private set; }

        public IExampleGameHelper ExampleGameHelper { get; }

        public ISaveLoadHelper SaveLoadHelper { get; }

        internal IDynamicModelService DynamicModelService { get; }

        private readonly IContainer Container;

        public GameInterface(IMessageBroker messageBroker)
        {
            MessageBroker = messageBroker;

            ContainerBuilder builder = new ContainerBuilder();
            builder.RegisterModule<GameInterfaceModule>();
            builder.RegisterModule<GameStateModule>();
            Container = builder.Build();

            DynamicModelService = Container.Resolve<IDynamicModelService>();
        }
    }
}
