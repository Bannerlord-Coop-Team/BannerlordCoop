using Common.Messaging;
using GameInterface.Services.CharacterCreation.Interfaces;
using GameInterface.Services.CharacterCreation.Messages;
using GameInterface.Services.GameState.Messages;
using GameInterface.Services.Heroes.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Heroes.Handlers
{
    internal class SaveHandler : IHandler
    {
        private readonly ISaveInterface saveInterface;
        private readonly IMessageBroker messageBroker;

        public SaveHandler(
            ISaveInterface saveInterface,
            IMessageBroker messageBroker)
        {
            this.saveInterface = saveInterface;
            this.messageBroker = messageBroker;

            messageBroker.Subscribe<PackageGameSaveData>(Handle);
        }

        private void Handle(MessagePayload<PackageGameSaveData> obj)
        {
            var tranferId = obj.What.TransfeId;
            var gameData = saveInterface.SaveCurrentGame();

            var packagedMessage = new GameSaveDataPackaged(tranferId, gameData);
            messageBroker.Publish(this, packagedMessage);
        }
    }
}
