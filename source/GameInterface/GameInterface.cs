using Autofac;
using GameInterface.Serialization;
using GameInterface.Services;
using HarmonyLib;
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
        private readonly ISerializationService _service;
        public GameInterface(ISerializationService serializationService)
        {
            _service = serializationService;

            Harmony harmony = new Harmony("com.Coop.GameInterface");
            harmony.PatchAll();

            ContainerBuilder builder = new ContainerBuilder();
            builder.RegisterModule<GameInterfaceModule>();
            IContainer container = builder.Build();

            IServiceModule serviceModule = container.Resolve<IServiceModule>();
            serviceModule.InstantiateServices(container);
        }
    }
}
