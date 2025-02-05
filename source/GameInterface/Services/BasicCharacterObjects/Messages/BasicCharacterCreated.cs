using Common.Messaging;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.Core;

namespace GameInterface.Services.BasicCharacterObjects.Messages
{
    internal class BasicCharacterCreated : IEvent
    {
        public BasicCharacterObject CharacterObject { get; }

        public BasicCharacterCreated(BasicCharacterObject characterObject)
        {
            CharacterObject = characterObject;
        }
    }
}
