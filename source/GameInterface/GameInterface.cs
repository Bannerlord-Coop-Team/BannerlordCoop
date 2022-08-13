using Autofac;
using GameInterface.Helpers;
using GameInterface.Serialization.DynamicModel;

namespace GameInterface
{
    public class GameInterface : IGameInterface
    {
        public IExampleGameHelper ExampleGameHelper { get; }

        public ISaveLoadHelper SaveLoadHelper { get; }

        internal IDynamicModelService DynamicModelService { get; }

        private readonly IContainer Container;

        public GameInterface()
        {
            ContainerBuilder builder = new ContainerBuilder();
            builder.RegisterModule<GameInterfaceModule>();
            Container = builder.Build();

            DynamicModelService = Container.Resolve<IDynamicModelService>();
        }
    }
}
