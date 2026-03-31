using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Battles.Messages
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