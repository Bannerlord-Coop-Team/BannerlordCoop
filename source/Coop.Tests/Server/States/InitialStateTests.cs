using Autofac;
using Common.Messaging;
using Coop.Core.Server;
using Coop.Core.Server.States;
using GameInterface.Registry;
using GameInterface.Services.GameState.Messages;
using GameInterface.Services.MapEvents;
using Moq;
using System.Collections.Generic;
using Xunit;
using Xunit.Abstractions;

namespace Coop.Tests.Server.States
{
    public class InitialStateTests
    {
        private readonly ServerTestComponent serverComponent;

        public InitialStateTests(ITestOutputHelper output)
        {
            serverComponent = new ServerTestComponent(output);

            var container = serverComponent.Container;
        }

        [Fact]
        public void InitialStateStart()
        {
            // Arrange
            IServerLogic serverLogic = serverComponent.Container.Resolve<IServerLogic>();

            // Act
            serverLogic.State.Start();

            var payload = new MessagePayload<CampaignReady>(null, new CampaignReady());
            var initialState = Assert.IsType<InitialServerState>(serverLogic.State);
            initialState.Handle_CampaignReady(payload);

            // Assert
            Assert.IsType<ServerRunningState>(serverLogic.State);
        }

        [Fact]
        public void InitialStateStop()
        {
            // Arrange
            IServerLogic serverLogic = serverComponent.Container.Resolve<IServerLogic>();

            // Act
            serverLogic.State.Stop();

            // Assert
            Assert.IsType<InitialServerState>(serverLogic.State);
        }

        [Fact]
        public void CampaignReady_CleansLoadedMapEventsBetweenRegistrationAndLifetimePatching()
        {
            var calls = new List<string>();
            var registryManager = serverComponent.Container.Resolve<Mock<IRegistryManager>>();
            var mapEventLoadCleaner = serverComponent.Container.Resolve<Mock<IMapEventLoadCleaner>>();
            registryManager.Setup(manager => manager.RegisterAllGameObjects())
                .Callback(() => calls.Add("register"));
            mapEventLoadCleaner.Setup(cleaner => cleaner.FinalizePlayerMapEvents())
                .Callback(() => calls.Add("clean"));
            registryManager.Setup(manager => manager.PatchLifetimes())
                .Callback(() => calls.Add("patch"));

            var initialState = Assert.IsType<InitialServerState>(
                serverComponent.Container.Resolve<IServerLogic>().State);
            initialState.Handle_CampaignReady(new MessagePayload<CampaignReady>(null, new CampaignReady()));

            Assert.Equal(new[] { "register", "clean", "patch" }, calls);
        }
    }
}
