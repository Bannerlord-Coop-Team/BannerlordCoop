using Common.Messaging;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;

namespace GameInterface.Services.CharacterObjects.Messages
{
    [ProtoContract(SkipConstructor = true)]
    internal class NetworkCreateCharacterObject : ICommand
    {
        [ProtoMember(1)]
        public string CharacterObjectId;

        public NetworkCreateCharacterObject(string characterObjectId)
        {
            CharacterObjectId = characterObjectId;
        }
    }
}
