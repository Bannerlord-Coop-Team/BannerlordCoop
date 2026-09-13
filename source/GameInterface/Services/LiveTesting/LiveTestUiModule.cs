#if DEBUG
using Autofac;

namespace GameInterface.Services.LiveTesting;

/// <summary>DEBUG UI services, also available before campaign container creation.</summary>
public sealed class LiveTestUiModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<GauntletWidgetAdapter>().As<IUiWidgetAdapter>().InstancePerDependency();
        builder.RegisterType<LiveTestUi>().As<ILiveTestUi>().InstancePerDependency();
    }
}
#endif
