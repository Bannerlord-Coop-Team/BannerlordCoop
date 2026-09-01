using Autofac;
using Coop.Core.Server;
using Coop.Core.Server.Services.Telemetry;
using Coop.Tests.Mocks;
using GameInterface.Registry;
using Xunit.Abstractions;

namespace Coop.Tests;

internal class ServerTestComponent : TestComponentBase
{
    public ServerTestComponent(ITestOutputHelper output) : base(output)
    {
        var builder = new ContainerBuilder();
        builder.RegisterModule<ServerModule>();
        builder.RegisterModule<RegistryModule>();
        builder.RegisterType<MockServerTelemetryUploader>()
            .As<IServerTelemetryUploader>()
            .As<IBattlesFoughtUploader>()
            .SingleInstance();

        RegisterMock<ICoopServer>(builder);

        Container = BuildContainer(builder);
    }
}
