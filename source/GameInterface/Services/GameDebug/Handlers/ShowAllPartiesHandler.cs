using Common.Messaging;
using GameInterface.Services.GameDebug.Interfaces;
using GameInterface.Services.GameDebug.Messages;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace GameInterface.Services.GameDebug.Handlers
{
    /// <summary>
    /// Handles the <see cref="ShowAllParties"/> event.
    /// </summary>
    internal class ShowAllPartiesHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly IDebugGameInterface debugGameInterface;

        public ShowAllPartiesHandler(IMessageBroker messageBroker,
            IDebugGameInterface debugGameInterface)
        {
            this.messageBroker = messageBroker;
            this.debugGameInterface = debugGameInterface;
            messageBroker.Subscribe<ShowAllParties>(Handle_ShowAllParties);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<ShowAllParties>(Handle_ShowAllParties);
        }

        private void Handle_ShowAllParties(MessagePayload<ShowAllParties> obj)
        {
            debugGameInterface.ShowAllParties();
        }
    }
}
