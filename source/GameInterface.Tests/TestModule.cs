using Autofac;
using GameInterface.Services.ObjectManager;
using GameInterface.Tests.Bootstrap.Modules;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameInterface.Tests
{
    class TestModule
    {
        public static IContainer Build()
        {
            ContainerBuilder builder = new ContainerBuilder();

            builder.RegisterModule<SerializationTestModule>();
            builder.RegisterModule<ObjectManagerModule>();

            return builder.Build();
        }
    }
}
