using Autofac;
using GameInterface.Serialization.DynamicModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameInterface
{
    internal class GameInterfaceModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);

            builder.RegisterType<DynamicModelGenerator>().As<IDynamicModelGenerator>().SingleInstance();
            builder.RegisterType<DynamicModelService>().As<IDynamicModelService>().SingleInstance();
        }
    }
}
