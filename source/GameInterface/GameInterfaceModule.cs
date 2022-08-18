using Autofac;
using GameInterface.Serialization.DynamicModel;

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
