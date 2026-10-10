using Autofac;
using Coop.Core.Client;
using Coop.Core.Client.States;
using Xunit;
using Xunit.Abstractions;

namespace Coop.Tests.Client
{
    public class ClientLogicTests
    {
        private readonly ITestOutputHelper output;

        public ClientLogicTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]
        public void RunningState_BeforeInitialization_ReturnsFalseWithoutCreatingState()
        {
            var logic = new ClientLogic(null);

            Assert.False(logic.RunningState);
            Assert.False(logic.RunningState);
        }

        [Fact]
        public void RunningState_TracksCampaignMissionAndMainMenu()
        {
            var component = new ClientTestComponent(output);
            var logic = component.Container.Resolve<IClientLogic>();

            Assert.False(logic.RunningState);
            logic.SetState<MainMenuState>();
            Assert.False(logic.RunningState);
            logic.SetState<CampaignState>();
            Assert.True(logic.RunningState);
            logic.SetState<MissionState>();
            Assert.True(logic.RunningState);
            logic.SetState<MainMenuState>();
            Assert.False(logic.RunningState);
        }
    }
}
