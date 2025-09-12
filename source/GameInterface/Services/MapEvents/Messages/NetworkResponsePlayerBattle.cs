using System;
using System.Collections.Generic;
using System.Text;
using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MapEvents.Messages
{
    [ProtoContract(SkipConstructor = true)]
    internal class NetworkResponsePlayerBattle : ICommand
    {
        [ProtoMember(1)]
        public string MapEventString { get; }

        public NetworkResponsePlayerBattle(string mapEventString)
        {
            MapEventString = mapEventString;
        }
    }
}
