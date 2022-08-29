using Coop.Core.Client;
using Coop.Core.Client.States;
using GameInterface.Services.GameState.Messages;
using Moq;
using NLog;
using Xunit;
using Xunit.Abstractions;

namespace Coop.Tests.Client.States
{
    public class MainMenuStateTests : CoopTest
    {
        private readonly ILogger logger;
        private readonly IClientLogic clientLogic;
        public MainMenuStateTests(ITestOutputHelper output) : base(output)
        {
            logger = new Mock<ILogger>().Object;
            clientLogic = new Mock<IClientLogic>().Object;
        }

        [Fact]
        public void Connect_CharacterNotCreated_EnterCharacterCreation()
        {
            var messageBroker = mockMessageBroker.Object;
            var mainMenuState = new MainMenuState(clientLogic, mockMessageBroker.Object);
            messageBroker.Publish(this, new Connected(false));
        }

        [Fact]
        public void ConnectHandler_CharacterCreated_EnterHell()
        {

        }

        [Fact]
        public void Dispose_RemovesAllHandlers()
        {

        }
    }
}
