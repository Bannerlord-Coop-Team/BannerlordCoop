using Autofac;
using GameInterface.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameInterface
{
    public class GameInterface : IGameInterface
    {
        public IContainer Container { get; }
        public GameInterface()
        {
            ContainerBuilder builder = new ContainerBuilder();
            builder.RegisterModule<GameInterfaceModule>();
            IContainer container = builder.Build();

            IServiceModule serviceModule = container.Resolve<IServiceModule>();
            serviceModule.InstantiateServices(container);
        }
    }
}
