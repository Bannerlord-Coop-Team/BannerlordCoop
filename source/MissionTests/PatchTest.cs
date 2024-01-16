using Autofac;
using Missions;
using Missions.Services;
using Missions.Services.Network;
using Xunit;

namespace MissionTests
{
    public class AutoFacTests
    {
        IContainer _container;
        public AutoFacTests()
        {
            ContainerBuilder builder = new ContainerBuilder();

            builder.RegisterModule<MissionModule>();

            _container = builder.Build();
        }

        [Fact]
        public void MissionModule_Resolve_CoopMissionNetworkBehavior()
        {
            var networkBehavior = _container.Resolve<CoopMissionNetworkBehavior>();

            Assert.NotNull(networkBehavior);
        }

        [Fact]
        public void MissionModule_Resolve_CoopArenaController()
        {
            var networkBehavior = _container.Resolve<CoopArenaController>();

            Assert.NotNull(networkBehavior);
        }
    }
}