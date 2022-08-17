using Common.Messages;
using GameInterface.Messages.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace GameInterface
{
    public interface IGameOrchestrator
    {

    }

    public class GameOrchestrator : IGameOrchestrator
    {
        private readonly IMessageBroker messageBroker;
        private readonly IGameInterface gameInterface;

        public GameOrchestrator(IMessageBroker messageBroker, IGameInterface gameInterface)
        {
            this.messageBroker = messageBroker;
            this.gameInterface = gameInterface;
            messageBroker.Subscribe<EnterMainMenuCommand>(a => GoToMainMenuHandler(a));
        }

        private async void GoToMainMenuHandler(MessagePayload<EnterMainMenuCommand> payload)
        {
            var command = payload.What;
            var task = Task.Run(() => gameInterface.ExampleGameHelper.GoToMainMenu());
            var returnedTask = await Task.WhenAny(task, Task.Delay(command.TimeOut));
            var success = task.Status == TaskStatus.RanToCompletion;
            messageBroker.Publish(this, new EnteredMainMenuResponse(success));
        }
    }
}
