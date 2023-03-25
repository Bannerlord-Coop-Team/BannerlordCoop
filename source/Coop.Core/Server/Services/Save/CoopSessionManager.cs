using Common.Logging;
using Common.Messaging;
using Coop.Core.Server.Services.Save.Data;
using Coop.Core.Server.Services.Save.Messages;
using GameInterface.Services.Save.Messages;
using Serilog;
using Serilog.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Coop.Core.Server.Services.Save
{
    internal interface ICoopSessionManager
    {
        CoopSession CurrentSession { get; }
    }

    internal class CoopSessionManager : ICoopSessionManager
    {
        private readonly ILogger Logger = LogManager.GetLogger<CoopSessionManager>();

        public CoopSession CurrentSession { get; private set; }
        private readonly IMessageBroker _messageBroker;
        private readonly ICoopSaveManager _saveManager;

        public CoopSessionManager(
            IMessageBroker messageBroker,
            ICoopSaveManager saveManager) 
        {
            _messageBroker = messageBroker;
            _saveManager = saveManager;

            _messageBroker.Subscribe<ObjectGuidsPackaged>(Handle_ObjectGuidsPackaged);
        }

        private Guid saveTransactionId;
        public void SaveSession(string uniqueGameId, TimeSpan timeout)
        {
            saveTransactionId = Guid.NewGuid();
            _messageBroker.Publish(this, new PackageObjectGuids(saveTransactionId));
            var packagedIds = WaitForResponse<ObjectGuidsPackaged>(transactionId, timeout);

            Guid sessionId = Guid.NewGuid();
            if (CurrentSession != null)
            {
                sessionId = CurrentSession.SessionId;
            }

            return new CoopSession()
            {
                SessionId = sessionId,
                UniqueGameId = uniqueGameId,
                ControlledHeroes = packagedIds.ControlledHeros,
                HeroStringIdToGuid = packagedIds.HeroIds,
                PartyStringIdToGuid = packagedIds.PartyIds
            };
        }

        private void Handle_ObjectGuidsPackaged(MessagePayload<ObjectGuidsPackaged> obj)
        {
            if (saveTransactionId == obj.What.TransactionID)
            {
                var package = obj.What;

                package.

                _saveManager.SaveCoopSession();
            }
        }

        public void LoadSession()
        {

        }
    }
}
